%namespace Theory.Layered
%parsertype CalcParser
%visibility internal
%YYSTYPE double
%token NUM

%%
line   : expr                { Result = $1; } ;
expr   : expr '+' term       { $$ = $1 + $3; }
       | expr '-' term       { $$ = $1 - $3; }
       | term                { $$ = $1; }
       ;
term   : term '*' factor     { $$ = $1 * $3; }
       | term '/' factor     { $$ = $1 / $3; }
       | factor              { $$ = $1; }
       ;
factor : '-' factor          { $$ = -$2; }
       | '(' expr ')'        { $$ = $2; }
       | NUM                 { $$ = $1; }
       ;
%%
public double Result;
public CalcParser(QUT.Gppg.AbstractScanner<double, QUT.Gppg.LexLocation> s) : base(s) { }
