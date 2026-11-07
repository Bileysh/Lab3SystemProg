using CCompiler;
using CCompiler.Parsing;

namespace CCompiler.Tests;

public class CompilerTests
{
    static string Example(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", name));

    static string Run(string src)
    {
        var res = Compiler.Compile(src);
        Assert.True(res.Ok, string.Join("\n", res.Errors.Select(e => $"{e.Line}: {e.Message}")));
        Assert.Null(res.Interpreter!.Error);
        return res.Interpreter.Stdout;
    }

    static string InMain(string body) => Run("int main() {\n" + body + "\nreturn 0; }");

    [Fact]
    public void GrammarTablesAreBuilt()
    {
        Assert.True(Compiler.Stats.Rules > 100);
        Assert.True(Compiler.Stats.States > 150);
    }

    [Theory]
    [InlineData("1 - 2 - 3", "-4")]
    [InlineData("2 + 3 * 4", "14")]
    [InlineData("(2 + 3) * 4", "20")]
    [InlineData("8 / 4 / 2", "1")]
    [InlineData("-2 + 5", "3")]
    [InlineData("2 * -3", "-6")]
    [InlineData("1 < 2 == 1", "1")]
    [InlineData("1 || 0 && 0", "1")]
    [InlineData("1 ? 2 : 0 ? 3 : 4", "2")]
    [InlineData("6 & 3 | 8", "10")]
    [InlineData("1 << 2 + 1", "8")]
    [InlineData("!0 + 1", "2")]
    [InlineData("7 / -2", "-3")]
    [InlineData("-7 % 3", "-1")]
    public void OperatorPrecedenceAndAssociativity(string expr, string expected) =>
        Assert.Equal(expected, InMain($"printf(\"%d\", {expr});"));

    [Fact]
    public void DanglingElseBindsToNearestIf() =>
        Assert.Equal("inner-else", InMain("if (1) if (0) printf(\"then\"); else printf(\"inner-else\");"));

    [Fact]
    public void AssignmentIsRightAssociative() =>
        Assert.Equal("5 5 5", InMain("int a, b, c; a = b = c = 5; printf(\"%d %d %d\", a, b, c);"));

    [Fact]
    public void SyntaxErrorReportsLine()
    {
        var res = Compiler.Compile(Example("syntax_error.c"));
        Assert.False(res.Ok);
        var e = Assert.Single(res.Errors);
        Assert.Equal("syntax", e.Stage);
        Assert.Equal(5, e.Line);
    }

    [Fact]
    public void LexerCollectsTokens()
    {
        var errors = new List<CompileError>();
        var tokens = new List<TokenInfo>();
        Compiler.Parse("int x = 0x1F + 'a';", errors, tokens);
        Assert.Equal(["INT", "ID", "'='", "INT_CONST", "'+'", "CHAR_CONST", "';'"], tokens.Select(t => t.Type));
    }

    [Fact]
    public void TraceContainsShiftAndReduce()
    {
        var res = Compiler.Compile("int main() { return 1 + 2; }", trace: true);
        Assert.Contains(res.Trace, l => l.StartsWith("Shifting token"));
        Assert.Contains(res.Trace, l => l.StartsWith("Reducing stack by rule"));
    }

    [Fact]
    public void SemanticErrorsAreFound()
    {
        var res = Compiler.Compile(Example("semantic_errors.c"));
        Assert.False(res.Ok);
        var msgs = string.Join("\n", res.Errors.Select(e => e.Message));
        Assert.Contains("Повторне оголошення 'x'", msgs);
        Assert.Contains("Неоголошений ідентифікатор 'y'", msgs);
        Assert.Contains("очікує 2 аргумент", msgs);
        Assert.Contains("lvalue", msgs);
        Assert.Contains("поза циклом", msgs);
        Assert.Contains("void не може повертати", msgs);
    }

    [Fact]
    public void ShadowingInNestedBlocks() =>
        Assert.Equal("1 2 1", InMain("int x = 1; printf(\"%d \", x); { int x = 2; printf(\"%d \", x); } printf(\"%d\", x);"));

    [Fact]
    public void IntConversionTruncates() =>
        Assert.Equal("3 3.500000", InMain("int i = 3.99; double d = 7 / 2.0; printf(\"%d %f\", i, d);"));

    [Fact]
    public void RecursionWorks() =>
        Assert.Equal("6765", Run("int fib(int n) { return n < 2 ? n : fib(n-1) + fib(n-2); } int main() { printf(\"%d\", fib(20)); return 0; }"));

    [Fact]
    public void RuntimeErrorIsReported()
    {
        var res = Compiler.Compile("int main() { int a[3]; a[5] = 1; return 0; }");
        Assert.True(res.Ok);
        Assert.Equal("runtime", res.Interpreter!.Error!.Stage);
    }

    [Fact]
    public void PrintfFormats() =>
        Assert.Equal("[  42|42   |0042|3.14|1.500e+02|0.0001|1e+06|x|ab|ff|%]",
            InMain("printf(\"[%4d|%-5d|%04d|%.2f|%.3e|%g|%g|%c|%s|%x|%%]\", 42, 42, 42, 3.14159, 150.0, 0.0001, 1000000.0, 'x', \"ab\", 255);"));

    public static TheoryData<string> Examples() =>
        new(Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "examples"), "*.c")
            .Select(Path.GetFileName).Where(f => !f!.Contains("error"))!);

    [Theory]
    [MemberData(nameof(Examples))]
    public void GeneratedJavaScriptMatchesInterpreter(string file)
    {
        var res = Compiler.Compile(Example(file), file);
        Assert.True(res.Ok);
        Assert.Null(res.Interpreter!.Error);
        var node = Compiler.RunNode(res.JavaScript!);
        if (node is null) return;
        Assert.Null(node.Error);
        Assert.Equal(res.Interpreter.Stdout, node.Stdout);
        Assert.Equal(res.Interpreter.ExitCode, node.ExitCode);
    }
}
