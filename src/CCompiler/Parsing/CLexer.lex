%namespace CCompiler.Parsing
%scannertype CScanner
%visibility internal
%tokentype Tokens
%option stack, minimize, parser, persistbuffer, noembedbuffers, unicode

D       [0-9]
L       [a-zA-Z_]
H       [0-9a-fA-F]
E       [eE][\+\-]?{D}+
WS      [ \t\r\f\v]

%%

"/*"([^*]|"*"+[^*/])*"*"+"/"   { }
"//"[^\n]*                      { }
{WS}+                           { }
\n                              { }

"int"       { return Tok(Tokens.INT); }
"float"     { return Tok(Tokens.FLOAT); }
"double"    { return Tok(Tokens.DOUBLE); }
"char"      { return Tok(Tokens.CHAR); }
"void"      { return Tok(Tokens.VOID); }
"long"      { return Tok(Tokens.LONG); }
"short"     { return Tok(Tokens.SHORT); }
"unsigned"  { return Tok(Tokens.UNSIGNED); }
"signed"    { return Tok(Tokens.SIGNED); }
"const"     { return Tok(Tokens.CONST); }
"bool"|"_Bool" { return Tok(Tokens.BOOL); }
"if"        { return Tok(Tokens.IF); }
"else"      { return Tok(Tokens.ELSE); }
"while"     { return Tok(Tokens.WHILE); }
"do"        { return Tok(Tokens.DO); }
"for"       { return Tok(Tokens.FOR); }
"return"    { return Tok(Tokens.RETURN); }
"break"     { return Tok(Tokens.BREAK); }
"continue"  { return Tok(Tokens.CONTINUE); }

{L}({L}|{D})*     { yylval.s = yytext; return Tok(Tokens.ID); }

({D}+"."{D}*|"."{D}+){E}?[fFlL]?  |
{D}+{E}[fFlL]?    { yylval.d = Lexemes.ParseFloat(yytext); return Tok(Tokens.FLOAT_CONST); }

0[xX]{H}+[uUlL]*  |
{D}+[uUlL]*       { yylval.i = Lexemes.ParseInt(yytext); return Tok(Tokens.INT_CONST); }

\'(\\.|[^\\'\n])+\'   { yylval.i = Lexemes.ParseChar(yytext); return Tok(Tokens.CHAR_CONST); }
\"(\\.|[^\\"\n])*\"   { yylval.s = Lexemes.Unescape(yytext.Substring(1, yytext.Length - 2)); return Tok(Tokens.STRING); }

"<<="   { return Tok(Tokens.SHL_ASSIGN); }
">>="   { return Tok(Tokens.SHR_ASSIGN); }
"++"    { return Tok(Tokens.INC); }
"--"    { return Tok(Tokens.DEC); }
"<<"    { return Tok(Tokens.SHL); }
">>"    { return Tok(Tokens.SHR); }
"<="    { return Tok(Tokens.LE); }
">="    { return Tok(Tokens.GE); }
"=="    { return Tok(Tokens.EQ); }
"!="    { return Tok(Tokens.NE); }
"&&"    { return Tok(Tokens.AND); }
"||"    { return Tok(Tokens.OR); }
"+="    { return Tok(Tokens.ADD_ASSIGN); }
"-="    { return Tok(Tokens.SUB_ASSIGN); }
"*="    { return Tok(Tokens.MUL_ASSIGN); }
"/="    { return Tok(Tokens.DIV_ASSIGN); }
"%="    { return Tok(Tokens.MOD_ASSIGN); }
"&="    { return Tok(Tokens.AND_ASSIGN); }
"|="    { return Tok(Tokens.OR_ASSIGN); }
"^="    { return Tok(Tokens.XOR_ASSIGN); }

[\-+*/%<>=!~&|^?:;,()\[\]{}]   { return Tok(yytext[0]); }

.       { UnknownChar(); }

%%
