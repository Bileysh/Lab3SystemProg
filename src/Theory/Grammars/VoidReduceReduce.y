%namespace Theory.VoidRR
%parsertype VoidParser
%visibility internal
%token VOID INT ID

%%
func       : ID '(' params ')' ;
params     : param_list | VOID |  ;
param_list : param_list ',' param | param ;
param      : type ID | type ;
type       : INT | VOID ;
%%
public VoidParser(QUT.Gppg.AbstractScanner<int, QUT.Gppg.LexLocation> s) : base(s) { }
