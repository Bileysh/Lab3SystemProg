using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using QUT.Gppg;

namespace CCompiler.Parsing;

internal partial class CParser
{
    public ProgramNode? Result { get; private set; }

    public CParser(CScanner scanner) : base(scanner) { }

    static Expr Bin(string op, Node l, Node r, LexLocation at) =>
        new BinOp(op, (Expr)l, (Expr)r) { Line = at.StartLine };

    static Expr Asg(string op, Node t, Node v, LexLocation at) =>
        new Assign(op, (Expr)t, (Expr)v) { Line = at.StartLine };

    static Expr Un(string op, Node e, LexLocation at) =>
        new UnaryOp(op, (Expr)e) { Line = at.StartLine };

    public static (int Rules, int States) TableSizes()
    {
        int Len(string field) =>
            (typeof(CParser).GetField(field, System.Reflection.BindingFlags.NonPublic |
                                             System.Reflection.BindingFlags.Static)?.GetValue(null) as Array)?.Length ?? 0;
        return (Len("rules") - 1, Len("states"));
    }
}

public struct SemValue
{
    public string s;
    public long i;
    public double d;
    public Node n;
    public List<Node> list;
    public TypeSpec type;
    public Declarator dcl;
    public List<Declarator> dcls;
    public List<long?> dims;
    public List<Param> ps;
}

public record TokenInfo(string Type, string Text, int Line, int Column);

public record CompileError(string Message, int Line, string Stage);

public static class Lexemes
{
    public static long ParseInt(string s)
    {
        s = s.TrimEnd('u', 'U', 'l', 'L');
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.Parse(s[2..], NumberStyles.HexNumber);
        if (s.Length > 1 && s[0] == '0')
            return Convert.ToInt64(s, 8);
        return long.Parse(s, CultureInfo.InvariantCulture);
    }

    public static double ParseFloat(string s) =>
        double.Parse(s.TrimEnd('f', 'F', 'l', 'L'), CultureInfo.InvariantCulture);

    public static long ParseChar(string s) => Unescape(s[1..^1])[0];

    public static string Unescape(string s) =>
        Regex.Replace(s, @"\\(x[0-9a-fA-F]+|[0-7]{1,3}|.)", m =>
        {
            string e = m.Groups[1].Value;
            if (e[0] == 'x') return ((char)Convert.ToInt32(e[1..], 16)).ToString();
            if (char.IsDigit(e[0])) return ((char)Convert.ToInt32(e, 8)).ToString();
            return e[0] switch
            {
                'n' => "\n", 't' => "\t", 'r' => "\r", 'a' => "\a", 'b' => "\b",
                'f' => "\f", 'v' => "\v", _ => e,
            };
        });

    public static string Escape(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
            sb.Append(c switch
            {
                '\n' => "\\n", '\t' => "\\t", '\r' => "\\r", '\0' => "\\0",
                '\\' => "\\\\", '"' => "\\\"", '\'' => "\\'", _ => c.ToString(),
            });
        return sb.ToString();
    }

    public static string FormatDouble(double v)
    {
        string r = v.ToString("R", CultureInfo.InvariantCulture);
        return r.Contains('.') || r.Contains('E') || r.Contains("Infinity") || r.Contains("NaN") ? r : r + ".0";
    }
}

public static class Preprocessor
{
    public static string Run(string src)
    {
        var defines = new Dictionary<string, string>();
        var lines = src.Replace("\r\n", "\n").Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            string t = lines[n].Trim();
            if (t.StartsWith('#'))
            {
                var m = Regex.Match(t, @"^#\s*define\s+([A-Za-z_]\w*)\s+(.*)$");
                if (m.Success)
                    defines[m.Groups[1].Value] = m.Groups[2].Value.Split("//")[0].Trim();
                lines[n] = "";
            }
            else if (defines.Count > 0)
                lines[n] = Substitute(lines[n], defines);
        }
        return string.Join("\n", lines);
    }

    static string Substitute(string line, Dictionary<string, string> defines)
    {
        var parts = Regex.Split(line, "(\"(?:\\\\.|[^\\\\\"])*\")");
        for (int i = 0; i < parts.Length; i += 2)
            for (int k = 0; k < 10; k++)
            {
                string next = Regex.Replace(parts[i], @"\b[A-Za-z_]\w*\b",
                    m => defines.TryGetValue(m.Value, out var v) ? v : m.Value);
                if (next == parts[i]) break;
                parts[i] = next;
            }
        return string.Concat(parts);
    }
}
