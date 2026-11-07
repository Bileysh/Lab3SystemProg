using System.Diagnostics;
using System.Text;
using CCompiler.CodeGen;
using CCompiler.Parsing;
using CCompiler.Runtime;
using CCompiler.Semantics;

namespace CCompiler;

public sealed record AstJson(string Name, string Kind, string Category, int Line,
    Dictionary<string, string> Info, string? Role, List<AstJson> Children);

public sealed record RunResult(string Stdout, long? ExitCode, CompileError? Error, long Steps, double Ms);

public sealed record GrammarStats(int Rules, int States, string Generator);

public sealed class CompileResult
{
    public bool Ok { get; set; }
    public List<CompileError> Errors { get; } = [];
    public List<CompileError> Warnings { get; } = [];
    public List<TokenInfo> Tokens { get; set; } = [];
    public AstJson? Ast { get; set; }
    public string? AstText { get; set; }
    public string? JavaScript { get; set; }
    public RunResult? Interpreter { get; set; }
    public List<string> Trace { get; set; } = [];
    public GrammarStats Grammar { get; set; } = Compiler.Stats;
    public double ParseMs { get; set; }
}

public static class Compiler
{
    public static GrammarStats Stats { get; } = MakeStats();

    static GrammarStats MakeStats()
    {
        var (rules, states) = CParser.TableSizes();
        return new GrammarStats(rules, states, "GPPG (LALR(1)) + GPLEX");
    }

    public static ProgramNode? Parse(string source, List<CompileError> errors, List<TokenInfo>? tokens = null,
        List<string>? trace = null, int traceLimit = 5000)
    {
        var scanner = new CScanner();
        scanner.SetSource(Preprocessor.Run(source), 0);
        var parser = new CParser(scanner);
        LimitedWriter? tw = null;
        if (trace is not null) parser.TraceOut = tw = new LimitedWriter(traceLimit);
        bool ok = parser.Parse();
        errors.AddRange(scanner.Errors);
        tokens?.AddRange(scanner.TokenList);
        if (tw is not null) trace!.AddRange(tw.Lines);
        if (!ok && errors.Count == 0) errors.Add(new CompileError("Синтаксична помилка", 0, "syntax"));
        return ok && errors.Count == 0 ? parser.Result : null;
    }

    public static CompileResult Compile(string source, string name = "input.c", bool interpret = true, bool trace = false)
    {
        var res = new CompileResult();
        var sw = Stopwatch.StartNew();
        var tokens = new List<TokenInfo>();
        var traceLines = trace ? new List<string>() : null;
        var program = Parse(source, res.Errors, tokens, traceLines);
        res.ParseMs = sw.Elapsed.TotalMilliseconds;
        res.Tokens = tokens;
        res.Trace = traceLines ?? [];
        if (program is null) return res;

        var sema = new Analyzer();
        sema.Analyze(program);
        res.Warnings.AddRange(sema.Warnings);
        res.Ast = ToJson(program, null);
        res.AstText = AstPrinter.Print(program);
        if (sema.Errors.Count > 0)
        {
            res.Errors.AddRange(sema.Errors);
            return res;
        }
        res.Ok = true;
        res.JavaScript = JsGenerator.Generate(program, name);
        if (interpret) res.Interpreter = Run(program);
        return res;
    }

    public static RunResult Run(ProgramNode program)
    {
        var interp = new Interpreter(program);
        long? exit = null;
        CompileError? error = null;
        var sw = Stopwatch.StartNew();
        var t = new Thread(() =>
        {
            try { exit = interp.Run(); }
            catch (CRuntimeException ex) { error = new CompileError(ex.Message, ex.Line, "runtime"); }
            catch (Exception ex) { error = new CompileError(ex.Message, 0, "runtime"); }
        }, 256 * 1024 * 1024);
        t.Start();
        t.Join();
        return new RunResult(interp.Output, exit, error, interp.Steps, sw.Elapsed.TotalMilliseconds);
    }

    public static RunResult? RunNode(string js, int timeoutMs = 10000)
    {
        string path = Path.Combine(Path.GetTempPath(), $"ccomp_{Guid.NewGuid():N}.js");
        File.WriteAllText(path, js, new UTF8Encoding(false));
        try
        {
            var psi = new ProcessStartInfo("node", $"\"{path}\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            var sw = Stopwatch.StartNew();
            using var p = Process.Start(psi);
            if (p is null) return null;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs))
            {
                p.Kill(true);
                return new RunResult("", null, new CompileError("Перевищено час виконання", 0, "runtime"), 0, timeoutMs);
            }
            string err = stderr.Result;
            return new RunResult(stdout.Result, p.ExitCode,
                err.Length > 0 ? new CompileError(err, 0, "runtime") : null, 0, sw.Elapsed.TotalMilliseconds);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static AstJson ToJson(Node n, string? role)
    {
        var info = new Dictionary<string, string>();
        switch (n)
        {
            case Expr { CType: { } t }:
                info["type"] = t.ToString();
                break;
            case VarDecl { CType: { } vt }:
                info["type"] = vt.ToString();
                break;
            case FuncDef { Sym: { } fs }:
                info["returns"] = fs.Type.ToString();
                info["params"] = string.Join(", ", fs.Params);
                break;
        }
        Symbol? sym = n switch { Ident i => i.Sym, VarDecl v => v.Sym, Param p => p.Sym, _ => null };
        if (sym is not null)
        {
            info["symbol"] = $"{(sym.IsGlobal ? "global " : "")}{sym.Kind.ToString().ToLower()} (рядок {sym.Line})";
            if (sym.JsName != sym.Name) info["js"] = sym.JsName;
        }
        var children = n.Children().Where(c => c.Child is not null).Select(c => ToJson(c.Child!, c.Role)).ToList();
        return new AstJson(n.Label, n.GetType().Name, n.Category, n.Line, info, role, children);
    }

    sealed class LimitedWriter(int limit) : TextWriter
    {
        public List<string> Lines { get; } = [];
        readonly StringBuilder cur = new();
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value == '\n')
            {
                if (Lines.Count < limit) Lines.Add(cur.ToString().TrimEnd());
                else if (Lines.Count == limit) Lines.Add("… (траса обрізана)");
                cur.Clear();
            }
            else if (value != '\r' && Lines.Count <= limit) cur.Append(value);
        }
    }
}

public static class AstPrinter
{
    public static string Print(Node n)
    {
        var sb = new StringBuilder();
        Walk(n, null, "", true, sb);
        return sb.ToString();
    }

    static void Walk(Node n, string? role, string indent, bool last, StringBuilder sb)
    {
        sb.Append(indent).Append(indent.Length == 0 ? "" : last ? "└─ " : "├─ ");
        if (role is not null) sb.Append(role).Append(": ");
        sb.Append(n.Label);
        if (n is Expr { CType: { } t }) sb.Append("   «").Append(t).Append('»');
        sb.Append('\n');
        var kids = n.Children().Where(c => c.Child is not null).ToList();
        string next = indent.Length == 0 ? " " : indent + (last ? "   " : "│  ");
        for (int i = 0; i < kids.Count; i++) Walk(kids[i].Child!, kids[i].Role, next, i == kids.Count - 1, sb);
    }
}
