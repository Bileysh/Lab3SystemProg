using CCompiler.Parsing;

namespace CCompiler.Semantics;

public sealed class CType(string @base, IEnumerable<long?>? dims = null)
{
    public string Base { get; } = @base;
    public List<long?> Dims { get; } = dims?.ToList() ?? [];

    public bool IsArray => Dims.Count > 0 || Base == "string";
    public bool IsFloat => Base == "float" && Dims.Count == 0;
    public bool IsVoid => Base == "void" && Dims.Count == 0;
    public bool IsScalar => !IsArray && Base != "void";

    public CType Element() => Base == "string" ? Char : new CType(Base, Dims.Skip(1));

    public override string ToString() =>
        Base == "string" ? "char[]" : Base + string.Concat(Dims.Select(d => d is null ? "[]" : $"[{d}]"));

    public static readonly CType Int = new("int"), Float = new("float"), Char = new("char"),
        Void = new("void"), String = new("string");
}

public enum SymbolKind { Var, Param, Func, Builtin }

public sealed class Symbol(string name, SymbolKind kind, CType type, bool isGlobal, int line)
{
    public string Name { get; } = name;
    public SymbolKind Kind { get; } = kind;
    public CType Type { get; } = type;
    public bool IsGlobal { get; } = isGlobal;
    public int Line { get; } = line;
    public bool IsConst { get; set; }
    public string JsName { get; set; } = name;
    public List<CType> Params { get; set; } = [];
    public bool VarArgs { get; set; }
    public bool Defined { get; set; }
    public override string ToString() => $"{Kind} {Name}: {Type}";
}

public sealed class Analyzer
{
    static readonly HashSet<string> JsReserved =
    [
        "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do",
        "else", "enum", "export", "extends", "false", "finally", "for", "function", "if", "import", "in",
        "instanceof", "new", "null", "return", "super", "switch", "this", "throw", "true", "try", "typeof",
        "var", "void", "while", "with", "yield", "let", "static", "implements", "interface", "package",
        "private", "protected", "public", "await", "arguments", "eval", "undefined", "NaN", "Infinity",
        "Math", "console", "globalThis", "process", "Number", "String", "Array", "Object", "self", "postMessage",
    ];

    static readonly Dictionary<string, (CType Ret, CType[]? Params)> Builtins = new()
    {
        ["printf"] = (CType.Int, null),
        ["puts"] = (CType.Int, [new CType("char", [null])]),
        ["putchar"] = (CType.Int, [CType.Int]),
        ["abs"] = (CType.Int, [CType.Int]),
        ["fabs"] = (CType.Float, [CType.Float]),
        ["sqrt"] = (CType.Float, [CType.Float]),
        ["pow"] = (CType.Float, [CType.Float, CType.Float]),
        ["floor"] = (CType.Float, [CType.Float]),
        ["ceil"] = (CType.Float, [CType.Float]),
        ["sin"] = (CType.Float, [CType.Float]),
        ["cos"] = (CType.Float, [CType.Float]),
        ["exp"] = (CType.Float, [CType.Float]),
        ["log"] = (CType.Float, [CType.Float]),
    };

    public List<CompileError> Errors { get; } = [];
    public List<CompileError> Warnings { get; } = [];

    readonly List<Dictionary<string, Symbol>> scopes = [new()];
    FuncDef? func;
    int loopDepth;
    readonly HashSet<string> usedJs = [];
    readonly HashSet<string> globalJs = [];
    int renameCounter;

    public Analyzer()
    {
        foreach (var (name, (ret, ps)) in Builtins)
            scopes[0][name] = new Symbol(name, SymbolKind.Builtin, ret, true, 0)
            {
                Params = ps?.ToList() ?? [], VarArgs = ps is null, Defined = true,
            };
    }

    void Error(string msg, Node? n) => Errors.Add(new CompileError(msg, n?.Line ?? 0, "semantic"));
    void Warn(string msg, Node? n) => Warnings.Add(new CompileError(msg, n?.Line ?? 0, "warning"));

    Symbol? Lookup(string name)
    {
        for (int i = scopes.Count - 1; i >= 0; i--)
            if (scopes[i].TryGetValue(name, out var s)) return s;
        return null;
    }

    void PushScope() => scopes.Add(new());
    void PopScope() => scopes.RemoveAt(scopes.Count - 1);

    Symbol Declare(string name, SymbolKind kind, CType type, Node node)
    {
        var scope = scopes[^1];
        bool isGlobal = scopes.Count == 1;
        if (scope.TryGetValue(name, out var old))
        {
            if (!(old.Kind == SymbolKind.Func && kind == SymbolKind.Func))
                Error($"Повторне оголошення '{name}' (раніше в рядку {old.Line})", node);
            return old;
        }
        var sym = new Symbol(name, kind, type, isGlobal, node.Line) { JsName = JsNameFor(name, isGlobal) };
        scope[name] = sym;
        return sym;
    }

    string JsNameFor(string name, bool isGlobal)
    {
        string baseName = JsReserved.Contains(name) || name.StartsWith('_') ? name + "_" : name;
        if (isGlobal)
        {
            globalJs.Add(baseName);
            return baseName;
        }
        string cand = baseName;
        if (Lookup(name) is not null)
            do cand = $"{baseName}_{++renameCounter}";
            while (usedJs.Contains(cand) || globalJs.Contains(cand));
        usedJs.Add(cand);
        return cand;
    }

    static long? ConstInt(Node? e) => e switch
    {
        Const { Kind: ConstKind.Int or ConstKind.Char } c => c.IntValue,
        UnaryOp { Op: "-", Postfix: false } u => -ConstInt(u.Operand),
        UnaryOp { Op: "+", Postfix: false } u => ConstInt(u.Operand),
        BinOp b when ConstInt(b.Left) is long l && ConstInt(b.Right) is long r => b.Op switch
        {
            "+" => l + r, "-" => l - r, "*" => l * r,
            "/" when r != 0 => l / r, "%" when r != 0 => l % r, "<<" => l << (int)r,
            _ => null,
        },
        _ => null,
    };

    public void Analyze(ProgramNode program)
    {
        foreach (var d in program.Decls)
        {
            if (d is FuncDef f) VisitFunction(f);
            else VisitDecl(d);
        }
        if (!scopes[0].TryGetValue("main", out var main) || main.Kind != SymbolKind.Func)
            Error("Не знайдено функцію main()", program);
    }

    void VisitFunction(FuncDef f)
    {
        var ret = new CType(f.Ret.Base);
        scopes[0].TryGetValue(f.Name, out var sym);
        if (sym is { Kind: SymbolKind.Builtin })
        {
            Error($"Ім'я '{f.Name}' зарезервоване стандартною бібліотекою", f);
            return;
        }
        var ptypes = f.Params.Select(p => new CType(p.Type.Base, p.Dims)).ToList();
        if (sym is null)
        {
            sym = Declare(f.Name, SymbolKind.Func, ret, f);
            sym.Params = ptypes;
        }
        else if (sym.Kind != SymbolKind.Func)
        {
            Error($"'{f.Name}' вже оголошено як змінну", f);
            return;
        }
        else if (sym.Params.Count != ptypes.Count)
            Error($"Оголошення '{f.Name}' не збігається з прототипом (кількість параметрів)", f);
        f.Sym = sym;
        if (f.Body is null) return;
        if (sym.Defined) Error($"Повторне визначення функції '{f.Name}'", f);
        sym.Defined = true;

        func = f;
        usedJs.Clear();
        PushScope();
        foreach (var p in f.Params)
        {
            if (p.Type.Base == "void" && p.Dims.Count == 0) Error("Параметр не може мати тип void", p);
            if (p.Name is null)
            {
                Error("У визначенні функції параметр повинен мати ім'я", p);
                continue;
            }
            p.Sym = Declare(p.Name, SymbolKind.Param, new CType(p.Type.Base, p.Dims), p);
        }
        foreach (var item in f.Body.Items) VisitStmt(item);
        PopScope();
        if (!ret.IsVoid && f.Name != "main" && !AlwaysReturns(f.Body))
            Warn($"Функція '{f.Name}' може завершитись без return", f);
        func = null;
    }

    static bool AlwaysReturns(Node? s) => s switch
    {
        Return => true,
        Compound c => c.Items.Any(AlwaysReturns),
        If i => i.Else is not null && AlwaysReturns(i.Then) && AlwaysReturns(i.Else),
        _ => false,
    };

    void VisitDecl(Node n)
    {
        if (n is DeclList dl)
        {
            foreach (var d in dl.Decls) VisitDecl(d);
            return;
        }
        var v = (VarDecl)n;
        if (v.Type.Base == "void") Error($"Змінна '{v.Name}' не може мати тип void", v);
        var dims = new List<long?>();
        foreach (var de in v.DimExprs)
        {
            if (de is null) { dims.Add(null); continue; }
            long? size = ConstInt(de);
            if (size is null or <= 0)
            {
                Error($"Розмір масиву '{v.Name}' має бути додатною константою", v);
                size = 1;
            }
            dims.Add(size);
        }
        if (v.Init is not null) VisitInit(v.Init, new CType(v.Type.Base, dims), v);
        if (dims.Count > 0 && dims[0] is null)
        {
            dims[0] = v.Init switch
            {
                InitList il => il.Items.Count,
                Const { Kind: ConstKind.String } s => s.StrValue.Length + 1,
                _ => null,
            };
            if (dims[0] is null) { Error($"Невідомий розмір масиву '{v.Name}'", v); dims[0] = 1; }
        }
        if (dims.Skip(1).Any(d => d is null))
        {
            Error($"Невідомий розмір масиву '{v.Name}'", v);
            dims = dims.Select(d => d ?? 1).Select(d => (long?)d).ToList();
        }
        v.CType = new CType(v.Type.Base, dims);
        if (v.Type.IsConst && v.Init is null) Warn($"Константа '{v.Name}' без ініціалізатора", v);
        v.Sym = Declare(v.Name, SymbolKind.Var, v.CType, v);
        v.Sym.IsConst = v.Type.IsConst;
    }

    void VisitInit(Node init, CType t, VarDecl v)
    {
        if (init is InitList il)
        {
            if (t.Dims.Count == 0) { Error($"Список ініціалізації для не-масиву '{v.Name}'", init); return; }
            if (t.Dims[0] is long n && il.Items.Count > n) Error($"Забагато ініціалізаторів для '{v.Name}'", init);
            foreach (var it in il.Items) VisitInit(it, t.Element(), v);
            return;
        }
        var et = Expr((Expr)init);
        if (t.Dims.Count > 0)
        {
            if (!(et.Base == "string" && t.Base == "char" && t.Dims.Count == 1))
                Error($"Масив '{v.Name}' ініціалізується списком {{...}}", init);
        }
        else if (et.IsArray) Error($"Не можна ініціалізувати скаляр '{v.Name}' масивом", init);
        else if (et.IsVoid) Error("Використання значення void-функції", init);
    }

    void VisitStmt(Node s)
    {
        switch (s)
        {
            case VarDecl or DeclList:
                VisitDecl(s);
                break;
            case Compound c:
                PushScope();
                foreach (var it in c.Items) VisitStmt(it);
                PopScope();
                break;
            case ExprStmt e:
                if (e.Expr is not null) Expr(e.Expr);
                break;
            case If i:
                Cond(i.Cond);
                Scoped(i.Then);
                if (i.Else is not null) Scoped(i.Else);
                break;
            case While w:
                Cond(w.Cond);
                Loop(w.Body);
                break;
            case DoWhile d:
                Loop(d.Body);
                Cond(d.Cond);
                break;
            case For f:
                PushScope();
                if (f.Init is VarDecl or DeclList) VisitDecl(f.Init);
                else if (f.Init is Expr ie) Expr(ie);
                if (f.Cond is not null) Cond(f.Cond);
                if (f.Step is not null) Expr(f.Step);
                Loop(f.Body);
                PopScope();
                break;
            case Return r:
                var ret = func!.Sym!.Type;
                if (r.Value is null)
                {
                    if (!ret.IsVoid) Warn($"return без значення у функції '{func.Name}' типу {ret}", r);
                }
                else
                {
                    var t = Expr(r.Value);
                    if (ret.IsVoid) Error($"Функція '{func.Name}' типу void не може повертати значення", r);
                    else if (!t.IsScalar) Error("Функція не може повертати масив або void", r);
                }
                break;
            case Break or Continue:
                if (loopDepth == 0) Error($"'{s.Label}' поза циклом", s);
                break;
            default:
                Error($"Невідомий оператор {s.GetType().Name}", s);
                break;
        }
    }

    void Scoped(Node s)
    {
        PushScope();
        VisitStmt(s);
        PopScope();
    }

    void Loop(Node body)
    {
        loopDepth++;
        Scoped(body);
        loopDepth--;
    }

    void Cond(Expr e)
    {
        if (!Expr(e).IsScalar) Error("Умова повинна мати скалярний тип", e);
    }

    CType Expr(Expr e) => e.CType = Compute(e);

    static readonly HashSet<string> IntOnly = ["%", "&", "|", "^", "<<", ">>"];
    static readonly HashSet<string> Relational = ["<", ">", "<=", ">=", "==", "!=", "&&", "||"];

    CType Compute(Expr e)
    {
        switch (e)
        {
            case Const c:
                return c.Kind switch
                {
                    ConstKind.Int => CType.Int, ConstKind.Float => CType.Float,
                    ConstKind.Char => CType.Char, _ => CType.String,
                };

            case Ident id:
            {
                var sym = Lookup(id.Name);
                if (sym is null) { Error($"Неоголошений ідентифікатор '{id.Name}'", id); return CType.Int; }
                if (sym.Kind is SymbolKind.Func or SymbolKind.Builtin)
                {
                    Error($"Функцію '{id.Name}' використано як змінну", id);
                    return CType.Int;
                }
                id.Sym = sym;
                if (sym.IsGlobal && func is not null) func.GlobalsUsed.Add(sym);
                return sym.Type;
            }

            case BinOp b:
            {
                var lt = Expr(b.Left);
                var rt = Expr(b.Right);
                if (!lt.IsScalar || !rt.IsScalar)
                {
                    Error($"Операція '{b.Op}' над масивом або void", b);
                    return CType.Int;
                }
                if (IntOnly.Contains(b.Op) && (lt.IsFloat || rt.IsFloat))
                    Error($"Операція '{b.Op}' вимагає цілих операндів", b);
                if (Relational.Contains(b.Op)) return CType.Int;
                if (b.Op is "/" or "%" && ConstInt(b.Right) == 0 && !rt.IsFloat) Warn("Ділення на нуль", b);
                return lt.IsFloat || rt.IsFloat ? CType.Float : CType.Int;
            }

            case UnaryOp u:
            {
                var t = Expr(u.Operand);
                if (!t.IsScalar)
                {
                    Error($"Унарна операція '{u.Op}' над масивом або void", u);
                    return CType.Int;
                }
                if (u.Op is "++" or "--") { CheckLvalue(u.Operand, u.Op); return t; }
                if (u.Op == "!") return CType.Int;
                if (u.Op == "~" && t.IsFloat) Error("Операція '~' вимагає цілого операнда", u);
                return t.IsFloat ? CType.Float : CType.Int;
            }

            case Assign a:
            {
                var tt = Expr(a.Target);
                var vt = Expr(a.Value);
                CheckLvalue(a.Target, a.Op);
                if (!vt.IsScalar) Error("Присвоєння масиву або void-значення", a);
                if (IntOnly.Contains(a.Op.TrimEnd('=')) && (tt.IsFloat || vt.IsFloat))
                    Error($"Операція '{a.Op}' вимагає цілих операндів", a);
                return tt;
            }

            case Ternary t:
            {
                Cond(t.Cond);
                var a = Expr(t.Then);
                var b = Expr(t.Else);
                return a.IsFloat || b.IsFloat ? CType.Float : a;
            }

            case Index ix:
            {
                var at = Expr(ix.Array);
                var it = Expr(ix.IndexExpr);
                if (!at.IsArray) { Error("Індексування не-масиву", ix); return CType.Int; }
                if (!it.IsScalar || it.IsFloat) Error("Індекс масиву повинен бути цілим", ix);
                return at.Element();
            }

            case Cast c:
                if (!Expr(c.Operand).IsScalar) Error("Приведення типу для масиву або void", c);
                return new CType(c.Type.Base);

            case Call call:
            {
                var sym = Lookup(call.Name);
                var argt = call.Args.Select(Expr).ToList();
                if (sym is null) { Error($"Виклик неоголошеної функції '{call.Name}'", call); return CType.Int; }
                if (sym.Kind is not (SymbolKind.Func or SymbolKind.Builtin))
                {
                    Error($"'{call.Name}' не є функцією", call);
                    return CType.Int;
                }
                call.Sym = sym;
                if (sym.VarArgs)
                {
                    if (argt.Count == 0 || !argt[0].IsArray)
                        Error($"Перший аргумент '{call.Name}' - рядок формату", call);
                }
                else if (argt.Count != sym.Params.Count)
                    Error($"Функція '{call.Name}' очікує {sym.Params.Count} аргумент(и), передано {argt.Count}", call);
                else
                    for (int i = 0; i < argt.Count; i++)
                    {
                        if (sym.Params[i].IsArray != argt[i].IsArray)
                            Error($"Аргумент {i + 1} функції '{call.Name}': очікується {sym.Params[i]}, отримано {argt[i]}", call);
                        else if (argt[i].IsVoid)
                            Error($"Аргумент {i + 1} функції '{call.Name}' має тип void", call);
                    }
                return sym.Type;
            }

            default:
                Error($"Невідомий вираз {e.GetType().Name}", e);
                return CType.Int;
        }
    }

    void CheckLvalue(Expr e, string op)
    {
        switch (e)
        {
            case Ident { Sym: null }:
                return;
            case Ident { Sym: { } s } when !s.Type.IsArray:
                if (s.IsConst) Error($"Зміна константи '{s.Name}'", e);
                return;
            case Index { CType.IsScalar: true }:
                return;
            default:
                Error($"Лівий операнд '{op}' не є змінною (lvalue)", e);
                return;
        }
    }
}
