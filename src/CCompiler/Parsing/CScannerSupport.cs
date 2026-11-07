using QUT.Gppg;

namespace CCompiler.Parsing;

internal sealed partial class CScanner
{
    public readonly List<CompileError> Errors = new();

    public readonly List<TokenInfo> TokenList = new();

    int Tok(Tokens t) => Tok((int)t);

    int Tok(int t)
    {
        yylloc = new LexLocation(tokLin, tokCol, tokELin, tokECol);
        string name = t < 127 ? $"'{(char)t}'" : ((Tokens)t).ToString();
        TokenList.Add(new TokenInfo(name, yytext, tokLin, tokCol));
        return t;
    }

    void UnknownChar() => Errors.Add(new CompileError($"Невідомий символ '{yytext}'", tokLin, "lexer"));

    public override void yyerror(string format, params object[] args)
    {
        string msg = (args.Length == 0 ? format : string.Format(format, args))
            .Replace("Syntax error, unexpected", "Синтаксична помилка: неочікувана лексема")
            .Replace("syntax error, unexpected", "Синтаксична помилка: неочікувана лексема")
            .Replace(", expecting", ", очікувалось:");
        Errors.Add(new CompileError(msg, tokLin, "syntax"));
    }
}
