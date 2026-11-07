using System.Text;
using CCompiler.Parsing;
using CCompiler.Semantics;

namespace CCompiler.Runtime;

public sealed class CRuntimeException(string message, int line) : Exception(message)
{
    public int Line { get; } = line;
}

public sealed class Interpreter(ProgramNode program)
{
    public const int MaxSteps = 5_000_000;
    const int MaxDepth = 1000;

    sealed class ReturnSignal(object? value) : Exception { public object? Value { get; } = value; }
    sealed class BreakSignal : Exception;
    sealed class ContinueSignal : Exception;

    readonly Dictionary<Symbol, object> globals = [];
    readonly Stack<Dictionary<Symbol, object>> frames = new();
    readonly Dictionary<Symbol, FuncDef> funcs = [];
    readonly StringBuilder stdout = new();

    public long Steps { get; private set; }
    public string Output => stdout.ToString();

    public long Run()
    {
        foreach (var d in program.Decls)
        {
            if (d is FuncDef { Body: not null } f) funcs[f.Sym!] = f;
            else if (d is VarDecl or DeclList) ExecDecl(d, globals);
        }
        var main = funcs.Values.First(f => f.Name == "main");
        return CFormat.ToLong(CallFunction(main, []) ?? 0L);
    }

    void Tick(Node n)
    {
        if (++Steps > MaxSteps)
            throw new CRuntimeException("Перевищено ліміт кроків (нескінченний цикл?)", n.Line);
    }

    static object Coerce(CType t, object v) =>
        t.IsFloat ? CFormat.ToDouble(v) : t.IsScalar ? CFormat.ToLong(v) : v;

    object? CallFunction(FuncDef f, List<object> args)
    {
        if (frames.Count >= MaxDepth)
            throw new CRuntimeException("Переповнення стеку викликів (занадто глибока рекурсія)", f.Line);
        var frame = new Dictionary<Symbol, object>();
        for (int i = 0; i < f.Params.Count; i++)
            frame[f.Params[i].Sym!] = Coerce(f.Params[i].Sym!.Type, args[i]);
        frames.Push(frame);
        var ret = f.Sym!.Type;
        try
        {
            foreach (var s in f.Body!.Items) Exec(s);
        }
        catch (ReturnSignal r)
        {
            return ret.IsVoid || r.Value is null ? null : Coerce(ret, r.Value);
        }
        finally
        {
            frames.Pop();
        }
        return ret.IsVoid ? null : 0L;
    }

    Dictionary<Symbol, object> FrameOf(Symbol s) => s.IsGlobal ? globals : frames.Peek();

    void ExecDecl(Node n, Dictionary<Symbol, object> frame)
    {
        if (n is DeclList dl)
        {
            foreach (var d in dl.Decls) ExecDecl(d, frame);
            return;
        }
        var v = (VarDecl)n;
        var t = v.CType!;
        if (t.Dims.Count > 0)
        {
            var arr = CArray.Create(t.Dims.Select(d => d!.Value).ToList(), t.Base == "float" ? 0.0 : 0L);
            if (v.Init is not null) FillArray(arr, v.Init, t);
            frame[v.Sym!] = arr;
        }
        else
            frame[v.Sym!] = Coerce(t, v.Init is null ? 0L : Eval((Expr)v.Init));
    }

    void FillArray(CArray arr, Node init, CType t)
    {
        if (init is Const { Kind: ConstKind.String } s)
        {
            for (int i = 0; i < s.StrValue.Length && i < arr.Items.Length; i++) arr.Items[i] = (long)s.StrValue[i];
            return;
        }
        var items = ((InitList)init).Items;
        var el = t.Element();
        for (int i = 0; i < items.Count && i < arr.Items.Length; i++)
        {
            if (el.Dims.Count > 0) FillArray((CArray)arr.Items[i], items[i], el);
            else arr.Items[i] = Coerce(el, Eval((Expr)items[i]));
        }
    }

    void Exec(Node s)
    {
        Tick(s);
        switch (s)
        {
            case ExprStmt e:
                if (e.Expr is not null) Eval(e.Expr);
                break;
            case VarDecl or DeclList:
                ExecDecl(s, frames.Peek());
                break;
            case Compound c:
                foreach (var it in c.Items) Exec(it);
                break;
            case If i:
                if (Truth(i.Cond)) Exec(i.Then);
                else if (i.Else is not null) Exec(i.Else);
                break;
            case While w:
                while (Truth(w.Cond))
                    if (!LoopBody(w.Body)) break;
                break;
            case DoWhile d:
                do
                    if (!LoopBody(d.Body)) break;
                while (Truth(d.Cond));
                break;
            case For f:
                if (f.Init is VarDecl or DeclList) ExecDecl(f.Init, frames.Peek());
                else if (f.Init is Expr ie) Eval(ie);
                while (f.Cond is null || Truth(f.Cond))
                {
                    if (!LoopBody(f.Body)) break;
                    if (f.Step is not null) Eval(f.Step);
                    Tick(f);
                }
                break;
            case Return r:
                throw new ReturnSignal(r.Value is null ? null : Eval(r.Value));
            case Break:
                throw new BreakSignal();
            case Continue:
                throw new ContinueSignal();
        }
    }

    bool LoopBody(Node body)
    {
        try { Exec(body); }
        catch (BreakSignal) { return false; }
        catch (ContinueSignal) { }
        return true;
    }

    bool Truth(Expr e) => Eval(e) switch { double d => d != 0, var v => CFormat.ToLong(v) != 0 };

    (Func<object> Get, Action<object> Set) Ref(Expr e)
    {
        if (e is Ident id)
        {
            var frame = FrameOf(id.Sym!);
            return (() => frame[id.Sym!], v => frame[id.Sym!] = v);
        }
        var ix = (Index)e;
        var arr = (CArray)Eval(ix.Array);
        long i = CFormat.ToLong(Eval(ix.IndexExpr));
        if (i < 0 || i >= arr.Items.Length)
            throw new CRuntimeException($"Індекс {i} поза межами масиву розміру {arr.Items.Length}", e.Line);
        return (() => arr.Items[i], v => arr.Items[i] = v);
    }

    object Eval(Expr e)
    {
        switch (e)
        {
            case Const c:
                return c.Kind switch
                {
                    ConstKind.Float => c.FloatValue, ConstKind.String => c.StrValue, _ => c.IntValue,
                };
            case Ident id:
                return FrameOf(id.Sym!)[id.Sym!];
            case Index:
                return Ref(e).Get();
            case BinOp b:
                if (b.Op == "&&") return Truth(b.Left) && Truth(b.Right) ? 1L : 0L;
                if (b.Op == "||") return Truth(b.Left) || Truth(b.Right) ? 1L : 0L;
                return Arith(b.Op, Eval(b.Left), Eval(b.Right), b.Left.CType!.IsFloat || b.Right.CType!.IsFloat, b.Line);
            case UnaryOp u when u.Op is "++" or "--":
            {
                var (get, set) = Ref(u.Operand);
                object old = get();
                object nv = Arith(u.Op == "++" ? "+" : "-", old, 1L, old is double, u.Line);
                set(nv);
                return u.Postfix ? old : nv;
            }
            case UnaryOp u:
            {
                object v = Eval(u.Operand);
                return u.Op switch
                {
                    "-" => v is double d ? -d : -CFormat.ToLong(v),
                    "+" => v,
                    "!" => (v is double d2 ? d2 == 0 : CFormat.ToLong(v) == 0) ? 1L : 0L,
                    _ => ~CFormat.ToLong(v),
                };
            }
            case Assign a:
            {
                var (get, set) = Ref(a.Target);
                object v = Eval(a.Value);
                if (a.Op != "=")
                    v = Arith(a.Op[..^1], get(), v, a.CType!.IsFloat || a.Value.CType!.IsFloat, a.Line);
                v = Coerce(a.CType!, v);
                set(v);
                return v;
            }
            case Ternary t:
            {
                object v = Truth(t.Cond) ? Eval(t.Then) : Eval(t.Else);
                return t.CType!.IsFloat ? CFormat.ToDouble(v) : v;
            }
            case Cast c:
                return Coerce(c.CType!, Eval(c.Operand));
            case Call call:
                return EvalCall(call);
        }
        throw new CRuntimeException($"Невідомий вираз {e.GetType().Name}", e.Line);
    }

    static object Arith(string op, object a, object b, bool isFloat, int line)
    {
        if (isFloat)
        {
            double x = CFormat.ToDouble(a), y = CFormat.ToDouble(b);
            return op switch
            {
                "+" => x + y, "-" => x - y, "*" => x * y, "/" => x / y,
                "<" => x < y ? 1L : 0L, ">" => x > y ? 1L : 0L, "<=" => x <= y ? 1L : 0L,
                ">=" => x >= y ? 1L : 0L, "==" => x == y ? 1L : 0L, "!=" => x != y ? 1L : 0L,
                _ => throw new CRuntimeException($"Операція {op} не визначена для float", line),
            };
        }
        long l = CFormat.ToLong(a), r = CFormat.ToLong(b);
        if (op is "/" or "%" && r == 0) throw new CRuntimeException("Ділення на нуль", line);
        return op switch
        {
            "+" => l + r, "-" => l - r, "*" => l * r, "/" => l / r, "%" => l % r,
            "<" => l < r ? 1L : 0L, ">" => l > r ? 1L : 0L, "<=" => l <= r ? 1L : 0L,
            ">=" => l >= r ? 1L : 0L, "==" => l == r ? 1L : 0L, "!=" => l != r ? 1L : 0L,
            "&" => l & r, "|" => l | r, "^" => l ^ r, "<<" => l << (int)r, ">>" => l >> (int)r,
            _ => throw new CRuntimeException($"Невідома операція {op}", line),
        };
    }

    object EvalCall(Call call)
    {
        var args = call.Args.Select(Eval).ToList();
        var sym = call.Sym!;
        if (sym.Kind == SymbolKind.Builtin)
        {
            double D(int i) => CFormat.ToDouble(args[i]);
            switch (sym.Name)
            {
                case "printf":
                {
                    string fmt = args[0] is CArray fa ? fa.AsString() : (string)args[0];
                    string s = CFormat.Format(fmt, args.Skip(1).ToList());
                    stdout.Append(s);
                    return (long)s.Length;
                }
                case "puts":
                    stdout.Append(args[0] is CArray pa ? pa.AsString() : (string)args[0]).Append('\n');
                    return 0L;
                case "putchar":
                    stdout.Append((char)CFormat.ToLong(args[0]));
                    return args[0];
                case "abs": return Math.Abs(CFormat.ToLong(args[0]));
                case "fabs": return Math.Abs(D(0));
                case "sqrt": return Math.Sqrt(D(0));
                case "pow": return Math.Pow(D(0), D(1));
                case "floor": return Math.Floor(D(0));
                case "ceil": return Math.Ceiling(D(0));
                case "sin": return Math.Sin(D(0));
                case "cos": return Math.Cos(D(0));
                case "exp": return Math.Exp(D(0));
                case "log": return Math.Log(D(0));
            }
        }
        if (!funcs.TryGetValue(sym, out var f))
            throw new CRuntimeException($"Функцію '{sym.Name}' оголошено, але не визначено", call.Line);
        Tick(call);
        return CallFunction(f, args) ?? 0L;
    }
}
