using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CCompiler.Runtime;

public sealed class CArray(object[] items)
{
    public object[] Items { get; } = items;

    public static CArray Create(IReadOnlyList<long> dims, object fill, int level = 0)
    {
        var items = new object[dims[level]];
        for (int i = 0; i < items.Length; i++)
            items[i] = level == dims.Count - 1 ? fill : Create(dims, fill, level + 1);
        return new CArray(items);
    }

    public string AsString()
    {
        var sb = new StringBuilder();
        foreach (var c in Items)
        {
            long code = Convert.ToInt64(c);
            if (code == 0) break;
            sb.Append((char)code);
        }
        return sb.ToString();
    }
}

public static partial class CFormat
{
    [GeneratedRegex(@"%([-+ #0]*)(\d+|\*)?(?:\.(\d*|\*))?(?:hh|h|ll|l|L|z|j|t)?([diouxXfFeEgGcs%])")]
    private static partial Regex Spec();

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Format(string fmt, IReadOnlyList<object?> args)
    {
        var sb = new StringBuilder();
        int argi = 0, pos = 0;
        object? Next() => argi < args.Count ? args[argi++] : 0L;

        foreach (Match m in Spec().Matches(fmt))
        {
            sb.Append(fmt, pos, m.Index - pos);
            pos = m.Index + m.Length;
            string flags = m.Groups[1].Value, conv = m.Groups[4].Value;
            if (conv == "%") { sb.Append('%'); continue; }
            int width = m.Groups[2].Value switch
            {
                "" => 0, "*" => (int)Convert.ToInt64(Next()), var w => int.Parse(w),
            };
            int? prec = !m.Groups[3].Success ? null : m.Groups[3].Value switch
            {
                "*" => (int)Convert.ToInt64(Next()), "" => 0, var p => int.Parse(p),
            };
            sb.Append(FormatOne(flags, width, prec, conv[0], Next()));
        }
        sb.Append(fmt, pos, fmt.Length - pos);
        return sb.ToString();
    }

    static string FormatOne(string flags, int width, int? prec, char conv, object? v)
    {
        bool left = flags.Contains('-'), zero = flags.Contains('0') && !left;
        bool plus = flags.Contains('+'), space = flags.Contains(' '), alt = flags.Contains('#');
        string body, sign = "";

        switch (conv)
        {
            case 'd' or 'i' or 'u':
            {
                long n = ToLong(v);
                if (n < 0) { sign = "-"; n = -n; } else if (plus) sign = "+"; else if (space) sign = " ";
                body = n.ToString(Inv);
                if (prec is int p) { body = body.PadLeft(p, '0'); zero = false; }
                break;
            }
            case 'o' or 'x' or 'X':
            {
                long n = ToLong(v);
                body = conv == 'o' ? Convert.ToString(n, 8) : n.ToString(conv == 'x' ? "x" : "X");
                if (prec is int p) { body = body.PadLeft(p, '0'); zero = false; }
                if (alt && n != 0) body = (conv == 'o' ? "0" : conv == 'x' ? "0x" : "0X") + body;
                break;
            }
            case 'f' or 'F' or 'e' or 'E' or 'g' or 'G':
            {
                double d = ToDouble(v);
                if (d < 0 || (d == 0 && double.IsNegative(d))) { sign = "-"; d = -d; }
                else if (plus) sign = "+"; else if (space) sign = " ";
                body = FormatFloat(d, prec ?? 6, conv, alt);
                if (double.IsInfinity(d) || double.IsNaN(d)) zero = false;
                break;
            }
            case 'c':
                body = v is string s1 ? s1[..1] : ((char)ToLong(v)).ToString();
                zero = false;
                break;
            default:
                body = v switch { CArray a => a.AsString(), null => "(null)", _ => Convert.ToString(v, Inv)! };
                if (prec is int ps && ps < body.Length) body = body[..ps];
                zero = false;
                break;
        }

        int padLen = width - sign.Length - body.Length;
        if (padLen <= 0) return sign + body;
        if (left) return sign + body + new string(' ', padLen);
        if (zero) return sign + new string('0', padLen) + body;
        return new string(' ', padLen) + sign + body;
    }

    public static string FormatFloat(double d, int prec, char conv, bool alt = false)
    {
        bool upper = char.IsUpper(conv);
        if (double.IsNaN(d)) return upper ? "NAN" : "nan";
        if (double.IsInfinity(d)) return upper ? "INF" : "inf";
        string r;
        switch (char.ToLower(conv))
        {
            case 'f':
                r = d.ToString("F" + prec, Inv);
                if (alt && prec == 0) r += ".";
                return r;
            case 'e':
                r = Exp(d, prec);
                return upper ? r.ToUpperInvariant() : r;
            default:
            {
                int p = prec == 0 ? 1 : prec;
                string es = Exp(d, p - 1);
                int x = int.Parse(es[(es.IndexOf('e') + 1)..], Inv);
                r = p > x && x >= -4 ? d.ToString("F" + (p - 1 - x), Inv) : es;
                if (!alt) r = StripZeros(r);
                return upper ? r.ToUpperInvariant() : r;
            }
        }
    }

    static string Exp(double d, int prec)
    {
        string s = d.ToString((prec > 0 ? "0." + new string('0', prec) : "0") + "e+00", Inv);
        return s;
    }

    static string StripZeros(string r)
    {
        int e = r.IndexOf('e');
        string mant = e >= 0 ? r[..e] : r, exp = e >= 0 ? r[e..] : "";
        if (mant.Contains('.')) mant = mant.TrimEnd('0').TrimEnd('.');
        return mant + exp;
    }

    public static long ToLong(object? v) => v switch
    {
        long l => l, double d => (long)d, int i => i, bool b => b ? 1 : 0, null => 0,
        _ => throw new InvalidOperationException("очікувалось число"),
    };

    public static double ToDouble(object? v) => v switch
    {
        long l => l, double d => d, int i => i, null => 0,
        _ => throw new InvalidOperationException("очікувалось число"),
    };
}
