using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CCompiler.Semantics;

namespace CCompiler.CodeGen;

public sealed class JsGenerator
{
    const int PAssign = 2, PCond = 2, POr = 3, PAnd = 4, PUnary = 14, PPostfix = 15, PAtom = 20;

    static readonly Dictionary<string, int> PBin = new()
    {
        ["|"] = 5, ["^"] = 6, ["&"] = 7, ["=="] = 8, ["!="] = 8,
        ["<"] = 9, [">"] = 9, ["<="] = 9, [">="] = 9, ["<<"] = 10, [">>"] = 10,
        ["+"] = 11, ["-"] = 11, ["*"] = 12, ["/"] = 12, ["%"] = 12,
    };

    static readonly HashSet<string> Cmp = ["<", ">", "<=", ">=", "==", "!="];

    static readonly Dictionary<string, string> BuiltinJs = new()
    {
        ["printf"] = "_printf", ["puts"] = "_puts", ["putchar"] = "_putchar", ["abs"] = "Math.abs",
        ["fabs"] = "Math.abs", ["sqrt"] = "Math.sqrt", ["pow"] = "Math.pow", ["floor"] = "Math.floor",
        ["ceil"] = "Math.ceil", ["sin"] = "Math.sin", ["cos"] = "Math.cos", ["exp"] = "Math.exp", ["log"] = "Math.log",
    };

    readonly StringBuilder sb = new();
    int depth;
    CType retType = CType.Void;

    public static string Runtime { get; } = LoadRuntime();

    static string LoadRuntime()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("CCompiler.CodeGen.runtime.js")!;
        return new StreamReader(s).ReadToEnd().Trim();
    }

    public static string Generate(ProgramNode program, string sourceName) =>
        new JsGenerator().Program(program);

    void Line(string text) => sb.Append(' ', depth * 2).Append(text).Append('\n');

    string Program(ProgramNode p)
    {
        sb.Append(Runtime).Append("\n\n");
        var globals = p.Decls.Where(d => d is VarDecl or DeclList).ToList();
        if (globals.Count > 0)
        {
            foreach (var g in globals) Decl(g);
            sb.Append('\n');
        }
        foreach (var f in p.Decls.OfType<FuncDef>().Where(f => f.Body is not null))
        {
            Function(f);
            sb.Append('\n');
        }
        Line("try {");
        Line("  _finish(main());");
        Line("} catch (e) {");
        Line("  _out.push(\"\\n[помилка виконання] \" + (e instanceof RangeError ? \"переповнення стеку\" : e.message) + \"\\n\");");
        Line("  _finish(1);");
        Line("}");
        return sb.ToString();
    }

    void Function(FuncDef f)
    {
        retType = f.Sym!.Type;
        string ps = string.Join(", ", f.Params.Select(p => p.Sym!.JsName));
        Line($"function {f.Sym.JsName}({ps}) {{");
        depth++;
        foreach (var s in f.Body!.Items) Stmt(s);
        depth--;
        Line("}");
    }

    void Decl(Node n) => Line(DeclText(n) + ";");

    string DeclText(Node n)
    {
        var decls = n is DeclList dl ? dl.Decls : [(VarDecl)n];
        bool isConst = decls.All(d => d.Type.IsConst && d.Init is not null);
        return (isConst ? "const " : "let ") + string.Join(", ", decls.Select(OneDecl));
    }

    string OneDecl(VarDecl d)
    {
        var t = d.CType!;
        string name = d.Sym!.JsName;
        if (t.Dims.Count > 0)
        {
            string dims = "[" + string.Join(", ", t.Dims) + "]";
            string fill = t.Base == "float" ? "0.0" : "0";
            string init = d.Init is null ? "" : ", " + InitValue(d.Init, t);
            return $"{name} = _arr({dims}, {fill}{init})";
        }
        return $"{name} = " + (d.Init is null ? "0" : Converted(t, (Expr)d.Init));
    }

    string InitValue(Node init, CType t)
    {
        if (init is InitList il)
        {
            var el = t.Element();
            return "[" + string.Join(", ", il.Items.Select(x => el.Dims.Count > 0 ? InitValue(x, el) : Converted(el, (Expr)x))) + "]";
        }
        return E((Expr)init, 0);
    }

    void Stmt(Node s)
    {
        switch (s)
        {
            case VarDecl or DeclList:
                Decl(s);
                break;
            case Compound c:
                Line("{");
                depth++;
                foreach (var it in c.Items) Stmt(it);
                depth--;
                Line("}");
                break;
            case ExprStmt e:
                Line(e.Expr is null ? ";" : E(e.Expr, 0) + ";");
                break;
            case If i:
                Line($"if ({Cond(i.Cond)}) {{");
                Body(i.Then);
                var els = i.Else;
                while (els is If ei)
                {
                    Line($"}} else if ({Cond(ei.Cond)}) {{");
                    Body(ei.Then);
                    els = ei.Else;
                }
                if (els is not null)
                {
                    Line("} else {");
                    Body(els);
                }
                Line("}");
                break;
            case While w:
                Line($"while ({Cond(w.Cond)}) {{");
                Body(w.Body);
                Line("}");
                break;
            case DoWhile d:
                Line("do {");
                Body(d.Body);
                Line($"}} while ({Cond(d.Cond)});");
                break;
            case For f:
                string init = f.Init switch
                {
                    VarDecl or DeclList => DeclText(f.Init),
                    Expr ie => E(ie, 0),
                    _ => "",
                };
                string cond = f.Cond is null ? "" : " " + Cond(f.Cond);
                string step = f.Step is null ? "" : " " + E(f.Step, 0);
                Line($"for ({init};{cond};{step}) {{");
                Body(f.Body);
                Line("}");
                break;
            case Return r:
                Line(r.Value is null ? "return;" : $"return {Converted(retType, r.Value)};");
                break;
            case Break:
                Line("break;");
                break;
            case Continue:
                Line("continue;");
                break;
        }
    }

    void Body(Node s)
    {
        depth++;
        if (s is Compound c) foreach (var it in c.Items) Stmt(it);
        else Stmt(s);
        depth--;
    }

    static string Wrap((string Code, int Prec) cp, int min) => cp.Prec >= min ? cp.Code : $"({cp.Code})";

    string E(Expr e, int min) => Wrap(Ex(e), min);

    string Cond(Expr e) => CondP(e).Code;

    (string Code, int Prec) CondP(Expr e) => e switch
    {
        BinOp { Op: "&&" } b => ($"{Wrap(CondP(b.Left), PAnd)} && {Wrap(CondP(b.Right), PAnd + 1)}", PAnd),
        BinOp { Op: "||" } b => ($"{Wrap(CondP(b.Left), POr)} || {Wrap(CondP(b.Right), POr + 1)}", POr),
        UnaryOp { Op: "!" } u => ($"!{Wrap(CondP(u.Operand), PUnary)}", PUnary),
        BinOp b when Cmp.Contains(b.Op) => Compare(b),
        _ => Ex(e),
    };

    (string, int) Compare(BinOp b)
    {
        int p = PBin[b.Op];
        string op = b.Op is "==" or "!=" ? b.Op + "=" : b.Op;
        return ($"{E(b.Left, p)} {op} {E(b.Right, p + 1)}", p);
    }

    string Converted(CType target, Expr e)
    {
        var cp = Ex(e);
        return target.IsScalar && !target.IsFloat && e.CType is { IsFloat: true } ? $"Math.trunc({cp.Code})" : cp.Code;
    }

    (string Code, int Prec) Arith(string op, (string, int) l, (string, int) r, bool intMath)
    {
        if (op == "/" && intMath) return ($"_idiv({l.Item1}, {r.Item1})", PAtom);
        if (op == "%") return ($"_imod({l.Item1}, {r.Item1})", PAtom);
        int p = PBin[op];
        return ($"{Wrap(l, p)} {op} {Wrap(r, p + 1)}", p);
    }

    (string Code, int Prec) Ex(Expr e)
    {
        switch (e)
        {
            case Const c:
                return c.Kind switch
                {
                    ConstKind.Int => (c.IntValue.ToString(CultureInfo.InvariantCulture), c.IntValue < 0 ? PUnary : PAtom),
                    ConstKind.Float => (FloatLit(c.FloatValue), PAtom),
                    ConstKind.Char => (c.IntValue.ToString(CultureInfo.InvariantCulture), PAtom),
                    _ => (JsonSerializer.Serialize(c.StrValue, JsonOpts), PAtom),
                };
            case Ident id:
                return (id.Sym!.JsName, PAtom);
            case Index ix:
                return ($"{E(ix.Array, PPostfix)}[{E(ix.IndexExpr, 0)}]", PAtom);
            case BinOp { Op: "&&" or "||" } b:
                return ($"{Wrap(CondP(b), PCond + 1)} ? 1 : 0", PCond);
            case BinOp b when Cmp.Contains(b.Op):
                return ($"Number({Compare(b).Item1})", PAtom);
            case BinOp b:
                return Arith(b.Op, Ex(b.Left), Ex(b.Right), !b.CType!.IsFloat);
            case UnaryOp { Op: "!" } u:
                return ($"Number({CondP(u).Code})", PAtom);
            case UnaryOp { Op: "++" or "--" } u:
                return u.Postfix ? ($"{E(u.Operand, PPostfix)}{u.Op}", PPostfix) : ($"{u.Op}{E(u.Operand, PUnary)}", PUnary);
            case UnaryOp u:
            {
                string inner = E(u.Operand, PUnary);
                string sep = inner.StartsWith(u.Op) ? " " : "";
                return ($"{u.Op}{sep}{inner}", PUnary);
            }
            case Assign a:
                return AssignExpr(a);
            case Ternary t:
                return ($"{Wrap(CondP(t.Cond), POr)} ? {E(t.Then, PAssign)} : {E(t.Else, PAssign)}", PCond);
            case Cast c:
                return c.CType!.IsFloat ? Ex(c.Operand) : ($"Math.trunc({E(c.Operand, 0)})", PAtom);
            case Call call:
            {
                var sym = call.Sym!;
                IEnumerable<string> args = sym.Kind == SymbolKind.Builtin
                    ? call.Args.Select(a => E(a, PAssign))
                    : call.Args.Select((a, i) => Converted(sym.Params[i], a));
                string fn = sym.Kind == SymbolKind.Builtin ? BuiltinJs[sym.Name] : sym.JsName;
                return ($"{fn}({string.Join(", ", args)})", PAtom);
            }
        }
        throw new InvalidOperationException($"codegen: невідомий вираз {e.GetType().Name}");
    }

    (string, int) AssignExpr(Assign a)
    {
        string target = E(a.Target, PPostfix);
        var tt = a.CType!;
        bool valueFloat = a.Value.CType!.IsFloat;
        if (a.Op == "=")
            return ($"{target} = {Wrap((Converted(tt, a.Value), PAssign), PAssign)}", PAssign);
        string op = a.Op[..^1];
        bool intMath = !tt.IsFloat && !valueFloat;
        if (op is "/" or "%" && intMath || !tt.IsFloat && valueFloat)
        {
            var calc = Arith(op, (target, PAtom), Ex(a.Value), intMath);
            string v = !tt.IsFloat && valueFloat ? $"Math.trunc({calc.Code})" : calc.Code;
            return ($"{target} = {v}", PAssign);
        }
        return ($"{target} {a.Op} {E(a.Value, PAssign)}", PAssign);
    }

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    static string FloatLit(double v) => Parsing.Lexemes.FormatDouble(v);
}
