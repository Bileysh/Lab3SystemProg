%namespace Theory.NoPrec
%parsertype CalcParser
%visibility internal
%YYSTYPE double
%token NUM

%%
line : expr                  { Result = $1; } ;
expr : expr '+' expr         { $$ = $1 + $3; }
     | expr '-' expr         { $$ = $1 - $3; }
     | expr '*' expr         { $$ = $1 * $3; }
     | expr '/' expr         { $$ = $1 / $3; }
     | '-' expr              { $$ = -$2; }
     | '(' expr ')'          { $$ = $2; }
     | NUM                   { $$ = $1; }
     ;
%%
public double Result;
public CalcParser(QUT.Gppg.AbstractScanner<double, QUT.Gppg.LexLocation> s) : base(s) { }
