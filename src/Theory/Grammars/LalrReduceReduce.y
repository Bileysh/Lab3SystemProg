%namespace Theory.LalrRR
%parsertype LalrParser
%visibility internal
%token A B C D E

%%
s : A x D | B y D | A y E | B x E ;
x : C ;
y : C ;
%%
public LalrParser(QUT.Gppg.AbstractScanner<int, QUT.Gppg.LexLocation> s) : base(s) { }
