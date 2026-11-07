using CCompiler.Parsing;
using CCompiler.Semantics;

namespace CCompiler;

public abstract class Node
{
    public int Line { get; set; }
    public virtual string Category => "node";
    public abstract string Label { get; }
    public virtual IEnumerable<(string Role, Node? Child)> Children() => [];
}

public sealed class TypeSpec : Node
{
    public List<string> Words { get; }
    public TypeSpec(string word) => Words = [word];
    public override string Category => "type";
    public override string Label => string.Join(" ", Words);

    public string Base =>
        Words.Contains("void") ? "void" :
        Words.Contains("float") || Words.Contains("double") ? "float" :
        Words.Contains("char") ? "char" : "int";

    public bool IsConst => Words.Contains("const");
}

public sealed class ProgramNode(List<Node> decls) : Node
{
    public List<Node> Decls { get; } = decls;
    public override string Category => "program";
    public override string Label => "Program";
    public override IEnumerable<(string, Node?)> Children() => Decls.Select(d => ("decl", (Node?)d));
}

public sealed class Param(TypeSpec type, string? name, List<long?> dims) : Node
{
    public TypeSpec Type { get; } = type;
    public string? Name { get; } = name;
    public List<long?> Dims { get; } = dims;
    public Symbol? Sym { get; set; }
    public override string Category => "decl";
    public override string Label =>
        $"Param {Type.Label} {Name ?? "?"}" + string.Concat(Dims.Select(d => d is null ? "[]" : $"[{d}]"));

    public static List<Param> NormalizeVoid(List<Param> ps) =>
        ps.Count == 1 && ps[0].Name is null && ps[0].Type.Base == "void" && ps[0].Dims.Count == 0 ? [] : ps;
}

public sealed class FuncDef(TypeSpec ret, string name, List<Param> ps, Compound? body) : Node
{
    public TypeSpec Ret { get; } = ret;
    public string Name { get; } = name;
    public List<Param> Params { get; } = ps;
    public Compound? Body { get; } = body;
    public Symbol? Sym { get; set; }
    public HashSet<Symbol> GlobalsUsed { get; } = [];
    public override string Category => "func";
    public override string Label => Body is null ? $"Prototype {Name}" : $"Function {Name}";
    public override IEnumerable<(string, Node?)> Children() =>
        new (string, Node?)[] { ("returns", Ret) }
            .Concat(Params.Select(p => ("param", (Node?)p)))
            .Append(("body", Body));
}

public sealed class Declarator(string name, int line)
{
    public string Name { get; } = name;
    public int Line { get; } = line;
    public List<Node?> Dims { get; } = [];
    public Node? Init { get; set; }
}

public sealed class VarDecl(TypeSpec type, string name, List<Node?> dimExprs, Node? init) : Node
{
    public TypeSpec Type { get; } = type;
    public string Name { get; } = name;
    public List<Node?> DimExprs { get; } = dimExprs;
    public Node? Init { get; } = init;
    public CType? CType { get; set; }
    public Symbol? Sym { get; set; }
    public override string Category => "decl";
    public override string Label => $"VarDecl {Name}" + string.Concat(DimExprs.Select(_ => "[]"));
    public override IEnumerable<(string, Node?)> Children() =>
        new (string, Node?)[] { ("type", Type) }
            .Concat(DimExprs.Where(d => d is not null).Select(d => ("size", d)))
            .Append(("init", Init));
}

public sealed class DeclList(List<VarDecl> decls) : Node
{
    public List<VarDecl> Decls { get; } = decls;
    public override string Category => "decl";
    public override string Label => "Declaration";
    public override IEnumerable<(string, Node?)> Children() => Decls.Select(d => ("var", (Node?)d));
}

public static class Declarations
{
    public static Node Make(TypeSpec t, List<Declarator> ds, int line)
    {
        var decls = ds.Select(d => new VarDecl(t, d.Name, d.Dims, d.Init) { Line = d.Line }).ToList();
        return decls.Count == 1 ? decls[0] : new DeclList(decls) { Line = line };
    }
}

public sealed class InitList(List<Node> items) : Node
{
    public List<Node> Items { get; } = items;
    public override string Category => "expr";
    public override string Label => "{ … }";
    public override IEnumerable<(string, Node?)> Children() => Items.Select(i => ("item", (Node?)i));
}

public abstract class Stmt : Node
{
    public override string Category => "stmt";
}

public sealed class Compound(List<Node> items) : Stmt
{
    public List<Node> Items { get; } = items;
    public override string Label => "Block { }";
    public override IEnumerable<(string, Node?)> Children() => Items.Select(i => ("item", (Node?)i));
}

public sealed class If(Expr cond, Node then, Node? els) : Stmt
{
    public Expr Cond { get; } = cond;
    public Node Then { get; } = then;
    public Node? Else { get; } = els;
    public override string Label => Else is null ? "if" : "if / else";
    public override IEnumerable<(string, Node?)> Children() => [("cond", Cond), ("then", Then), ("else", Else)];
}

public sealed class While(Expr cond, Node body) : Stmt
{
    public Expr Cond { get; } = cond;
    public Node Body { get; } = body;
    public override string Label => "while";
    public override IEnumerable<(string, Node?)> Children() => [("cond", Cond), ("body", Body)];
}

public sealed class DoWhile(Node body, Expr cond) : Stmt
{
    public Node Body { get; } = body;
    public Expr Cond { get; } = cond;
    public override string Label => "do … while";
    public override IEnumerable<(string, Node?)> Children() => [("body", Body), ("cond", Cond)];
}

public sealed class For(Node? init, Expr? cond, Expr? step, Node body) : Stmt
{
    public Node? Init { get; } = init;
    public Expr? Cond { get; } = cond;
    public Expr? Step { get; } = step;
    public Node Body { get; } = body;
    public override string Label => "for";
    public override IEnumerable<(string, Node?)> Children() =>
        [("init", Init), ("cond", Cond), ("step", Step), ("body", Body)];
}

public sealed class Return(Expr? value) : Stmt
{
    public Expr? Value { get; } = value;
    public override string Label => "return";
    public override IEnumerable<(string, Node?)> Children() => [("value", Value)];
}

public sealed class Break : Stmt
{
    public override string Label => "break";
}

public sealed class Continue : Stmt
{
    public override string Label => "continue";
}

public sealed class ExprStmt(Expr? expr) : Stmt
{
    public Expr? Expr { get; } = expr;
    public override string Label => Expr is null ? "; (empty)" : "ExprStmt";
    public override IEnumerable<(string, Node?)> Children() => [("expr", Expr)];
}

public abstract class Expr : Node
{
    public CType? CType { get; set; }
    public override string Category => "expr";
}

public enum ConstKind { Int, Float, Char, String }

public sealed class Const : Expr
{
    public ConstKind Kind { get; init; }
    public long IntValue { get; init; }
    public double FloatValue { get; init; }
    public string StrValue { get; init; } = "";
    public string Raw { get; init; } = "";
    public override string Category => "literal";
    public override string Label => Raw;

    public static Const Int(long v, int line) => new() { Kind = ConstKind.Int, IntValue = v, Raw = v.ToString(), Line = line };
    public static Const Float(double v, int line) =>
        new() { Kind = ConstKind.Float, FloatValue = v, Raw = Lexemes.FormatDouble(v), Line = line };
    public static Const Char(long v, int line) =>
        new() { Kind = ConstKind.Char, IntValue = v, Raw = "'" + Lexemes.Escape(((char)v).ToString()) + "'", Line = line };
    public static Const Str(string v, int line) =>
        new() { Kind = ConstKind.String, StrValue = v, Raw = "\"" + Lexemes.Escape(v) + "\"", Line = line };
}

public sealed class Ident(string name) : Expr
{
    public string Name { get; } = name;
    public Symbol? Sym { get; set; }
    public override string Category => "ident";
    public override string Label => Name;
}

public sealed class BinOp(string op, Expr left, Expr right) : Expr
{
    public string Op { get; } = op;
    public Expr Left { get; } = left;
    public Expr Right { get; } = right;
    public override string Label => Op;
    public override IEnumerable<(string, Node?)> Children() => [("left", Left), ("right", Right)];
}

public sealed class UnaryOp(string op, Expr operand, bool postfix = false) : Expr
{
    public string Op { get; } = op;
    public Expr Operand { get; } = operand;
    public bool Postfix { get; } = postfix;
    public override string Label => Postfix ? $"x{Op} (postfix)" : $"{Op}x";
    public override IEnumerable<(string, Node?)> Children() => [("operand", Operand)];
}

public sealed class Assign(string op, Expr target, Expr value) : Expr
{
    public string Op { get; } = op;
    public Expr Target { get; } = target;
    public Expr Value { get; } = value;
    public override string Label => Op;
    public override IEnumerable<(string, Node?)> Children() => [("target", Target), ("value", Value)];
}

public sealed class Ternary(Expr cond, Expr then, Expr els) : Expr
{
    public Expr Cond { get; } = cond;
    public Expr Then { get; } = then;
    public Expr Else { get; } = els;
    public override string Label => "? :";
    public override IEnumerable<(string, Node?)> Children() => [("cond", Cond), ("then", Then), ("else", Else)];
}

public sealed class Call(string name, List<Node> args) : Expr
{
    public string Name { get; } = name;
    public List<Expr> Args { get; } = args.Cast<Expr>().ToList();
    public Symbol? Sym { get; set; }
    public override string Label => $"call {Name}()";
    public override IEnumerable<(string, Node?)> Children() => Args.Select(a => ("arg", (Node?)a));
}

public sealed class Index(Expr array, Expr index) : Expr
{
    public Expr Array { get; } = array;
    public Expr IndexExpr { get; } = index;
    public override string Label => "[ ]";
    public override IEnumerable<(string, Node?)> Children() => [("array", Array), ("index", IndexExpr)];
}

public sealed class Cast(TypeSpec type, Expr expr) : Expr
{
    public TypeSpec Type { get; } = type;
    public Expr Operand { get; } = expr;
    public override string Label => $"({Type.Label})";
    public override IEnumerable<(string, Node?)> Children() => [("expr", Operand)];
}
