using System.Text;
using QUT.Gppg;

Console.OutputEncoding = Encoding.UTF8;
string dir = Path.Combine(AppContext.BaseDirectory, "Grammars");

void Title(string t)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 78));
    Console.WriteLine(t);
    Console.WriteLine(new string('=', 78));
}

void Conflicts(string grammar)
{
    string path = Path.Combine(AppContext.BaseDirectory, grammar + ".conflicts");
    var lines = File.Exists(path)
        ? File.ReadAllLines(path).Where(l => !l.StartsWith("//") && l.Trim().Length > 0).ToArray()
        : [];
    int sr = lines.Count(l => l.Contains("Shift/Reduce conflict", StringComparison.OrdinalIgnoreCase));
    int rr = lines.Count(l => l.Contains("Reduce/Reduce conflict", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"  {grammar}.y: конфліктів shift/reduce = {sr}, reduce/reduce = {rr}   (деталі: {grammar}.conflicts)");
    if (lines.Length == 0) return;
    foreach (var l in lines.Take(14)) Console.WriteLine("    | " + l);
    if (lines.Length > 14) Console.WriteLine($"    | … ще {lines.Length - 14} рядків");
}

string[] exprs = ["1 - 2 - 3", "2 * 3 + 4", "8 / 4 / 2", "-2 + 5", "2 + 3 * 4"];

Title("1. Калькулятор з таблицею пріоритетів (%left '+' '-'  %left '*' '/'  %right UMINUS)");
Conflicts("ExprPrec");
foreach (var e in exprs)
{
    var p = new Theory.Prec.CalcParser(new CalcScanner(e, (int)Theory.Prec.Tokens.NUM, (int)Theory.Prec.Tokens.EOF));
    p.Parse();
    Console.WriteLine($"  {e,-12} = {p.Result}");
}

Title("2. Та сама граматика БЕЗ пріоритетів: конфлікти розв'язуються на користь SHIFT");
Conflicts("ExprNoPrec");
foreach (var e in exprs)
{
    var p = new Theory.NoPrec.CalcParser(new CalcScanner(e, (int)Theory.NoPrec.Tokens.NUM, (int)Theory.NoPrec.Tokens.EOF));
    p.Parse();
    Console.WriteLine($"  {e,-12} = {p.Result}   (розбір праворуч: як {RightAssoc(e)})");
}
Console.WriteLine("""
  Чому так: у стані з пунктами  expr -> expr '-' expr .   та   expr -> expr . '-' expr
  на вході '-' можна і згорнути (reduce, ліва асоціативність), і перенести (shift).
  Без %left yacc за замовчуванням робить shift -> операція групується праворуч.
""");

Title("3. Однозначна «шарова» граматика expr -> term -> factor (без %left)");
Conflicts("Layered");
foreach (var e in exprs)
{
    var p = new Theory.Layered.CalcParser(new CalcScanner(e, (int)Theory.Layered.Tokens.NUM, (int)Theory.Layered.Tokens.EOF));
    p.Parse();
    Console.WriteLine($"  {e,-12} = {p.Result}");
}

Title("4. «Висячий else» без %prec - shift/reduce, розв'язується shift (else -> найближчий if)");
Conflicts("DanglingElse");

Title("5. Граматика LR(1), але не LALR(1) - злиття станів дає reduce/reduce");
Conflicts("LalrReduceReduce");

Title("6. Reduce/reduce, знайдений під час розробки граматики C:  f(void)");
Conflicts("VoidReduceReduce");

Console.WriteLine($"\nHTML-звіти зі станами автоматів: {Path.Combine(dir, "*.report.html")}");

static string RightAssoc(string e) => e switch
{
    "1 - 2 - 3" => "1 - (2 - 3)",
    "2 * 3 + 4" => "2 * (3 + 4)",
    "8 / 4 / 2" => "8 / (4 / 2)",
    "-2 + 5" => "-(2 + 5)",
    _ => "2 + (3 * 4)",
};

sealed class CalcScanner(string text, int numToken, int eofToken) : AbstractScanner<double, LexLocation>
{
    int pos;

    public override int yylex()
    {
        while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
        if (pos >= text.Length) return eofToken;
        if (char.IsDigit(text[pos]))
        {
            int start = pos;
            while (pos < text.Length && (char.IsDigit(text[pos]) || text[pos] == '.')) pos++;
            yylval = double.Parse(text[start..pos], System.Globalization.CultureInfo.InvariantCulture);
            return numToken;
        }
        return text[pos++];
    }
}
