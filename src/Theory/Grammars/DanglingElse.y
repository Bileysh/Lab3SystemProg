%namespace Theory.Dangling
%parsertype DanglingParser
%visibility internal
%token IF ELSE C S

%%
stmt : IF C stmt
     | IF C stmt ELSE stmt
     | S
     ;
%%
public DanglingParser(QUT.Gppg.AbstractScanner<int, QUT.Gppg.LexLocation> s) : base(s) { }
