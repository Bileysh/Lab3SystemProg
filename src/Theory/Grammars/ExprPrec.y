%namespace Theory.Prec
%parsertype CalcParser
%visibility internal
%YYSTYPE double
%token NUM

%left '+' '-'
%left '*' '/'
%right UMINUS

%%
line : expr                  { Result = $1; } ;
expr : expr '+' expr         { $$ = $1 + $3; }
     | expr '-' expr         { $$ = $1 - $3; }
     | expr '*' expr         { $$ = $1 * $3; }
     | expr '/' expr         { $$ = $1 / $3; }
     | '-' expr %prec UMINUS { $$ = -$2; }
     | '(' expr ')'          { $$ = $2; }
     | NUM                   { $$ = $1; }
     ;
%%
public double Result;
public CalcParser(QUT.Gppg.AbstractScanner<double, QUT.Gppg.LexLocation> s) : base(s) { }
