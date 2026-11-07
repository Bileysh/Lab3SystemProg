%namespace CCompiler.Parsing
%partial
%parsertype CParser
%visibility internal
%tokentype Tokens

%YYSTYPE SemValue

%token <s> ID STRING
%token <i> INT_CONST CHAR_CONST
%token <d> FLOAT_CONST
%token INT FLOAT DOUBLE CHAR VOID LONG SHORT UNSIGNED SIGNED CONST BOOL
%token IF ELSE WHILE DO FOR RETURN BREAK CONTINUE
%token INC DEC SHL SHR LE GE EQ NE AND OR
%token ADD_ASSIGN SUB_ASSIGN MUL_ASSIGN DIV_ASSIGN MOD_ASSIGN
%token AND_ASSIGN OR_ASSIGN XOR_ASSIGN SHL_ASSIGN SHR_ASSIGN

%nonassoc IFX
%nonassoc ELSE
%right '=' ADD_ASSIGN SUB_ASSIGN MUL_ASSIGN DIV_ASSIGN MOD_ASSIGN AND_ASSIGN OR_ASSIGN XOR_ASSIGN SHL_ASSIGN SHR_ASSIGN
%right '?' ':'
%left OR
%left AND
%left '|'
%left '^'
%left '&'
%left EQ NE
%left '<' '>' LE GE
%left SHL SHR
%left '+' '-'
%left '*' '/' '%'
%right UNARY
%left INC DEC '['

%type <n> program external function_def declaration
%type <n> statement compound_stmt expr_stmt selection_stmt iteration_stmt jump_stmt
%type <n> block_item expr expr_opt for_init initializer
%type <list> external_list block_items arg_list init_list
%type <type> type_spec
%type <s> type_word string_list
%type <dcl> declarator init_declarator
%type <dcls> init_declarator_list
%type <dims> param_dims
%type <ps> param_list_opt param_list
%type <n> param

%start program

%%

program
    : external_list                 { $$ = Result = new ProgramNode($1) { Line = 1 }; }
    ;

external_list
    : external_list external        { $1.Add($2); $$ = $1; }
    | external                      { $$ = new List<Node> { $1 }; }
    ;

external
    : function_def                  { $$ = $1; }
    | declaration                   { $$ = $1; }
    ;

function_def
    : type_spec ID '(' param_list_opt ')' compound_stmt
                                    { $$ = new FuncDef($1, $2, $4, (Compound)$6) { Line = @2.StartLine }; }
    | type_spec ID '(' param_list_opt ')' ';'
                                    { $$ = new FuncDef($1, $2, $4, null) { Line = @2.StartLine }; }
    ;

param_list_opt
    : param_list                    { $$ = Param.NormalizeVoid($1); }
    |                    { $$ = new List<Param>(); }
    ;

param_list
    : param_list ',' param          { $1.Add((Param)$3); $$ = $1; }
    | param                         { $$ = new List<Param> { (Param)$1 }; }
    ;

param
    : type_spec ID param_dims       { $$ = new Param($1, $2, $3) { Line = @2.StartLine }; }
    | type_spec                     { $$ = new Param($1, null, new List<long?>()) { Line = @1.StartLine }; }
    ;

param_dims
    : param_dims '[' ']'            { $1.Add(null); $$ = $1; }
    | param_dims '[' INT_CONST ']'  { $1.Add($3); $$ = $1; }
    |                    { $$ = new List<long?>(); }
    ;

type_spec
    : type_spec type_word           { $1.Words.Add($2); $$ = $1; }
    | type_word                     { $$ = new TypeSpec($1) { Line = @1.StartLine }; }
    ;

type_word
    : INT       { $$ = "int"; }      | FLOAT    { $$ = "float"; }
    | DOUBLE    { $$ = "double"; }   | CHAR     { $$ = "char"; }
    | VOID      { $$ = "void"; }     | LONG     { $$ = "long"; }
    | SHORT     { $$ = "short"; }    | UNSIGNED { $$ = "unsigned"; }
    | SIGNED    { $$ = "signed"; }   | CONST    { $$ = "const"; }
    | BOOL      { $$ = "bool"; }
    ;

declaration
    : type_spec init_declarator_list ';'   { $$ = Declarations.Make($1, $2, @1.StartLine); }
    ;

init_declarator_list
    : init_declarator_list ',' init_declarator   { $1.Add($3); $$ = $1; }
    | init_declarator                            { $$ = new List<Declarator> { $1 }; }
    ;

init_declarator
    : declarator                    { $$ = $1; }
    | declarator '=' initializer    { $1.Init = $3; $$ = $1; }
    ;

declarator
    : ID                            { $$ = new Declarator($1, @1.StartLine); }
    | declarator '[' expr ']'       { $1.Dims.Add($3); $$ = $1; }
    | declarator '[' ']'            { $1.Dims.Add(null); $$ = $1; }
    ;

initializer
    : expr                          { $$ = $1; }
    | '{' init_list '}'             { $$ = new InitList($2) { Line = @1.StartLine }; }
    | '{' init_list ',' '}'         { $$ = new InitList($2) { Line = @1.StartLine }; }
    ;

init_list
    : init_list ',' initializer     { $1.Add($3); $$ = $1; }
    | initializer                   { $$ = new List<Node> { $1 }; }
    ;

compound_stmt
    : '{' block_items '}'           { $$ = new Compound($2) { Line = @1.StartLine }; }
    ;

block_items
    : block_items block_item        { $1.Add($2); $$ = $1; }
    |                    { $$ = new List<Node>(); }
    ;

block_item
    : declaration                   { $$ = $1; }
    | statement                     { $$ = $1; }
    ;

statement
    : compound_stmt                 { $$ = $1; }
    | expr_stmt                     { $$ = $1; }
    | selection_stmt                { $$ = $1; }
    | iteration_stmt                { $$ = $1; }
    | jump_stmt                     { $$ = $1; }
    ;

expr_stmt
    : expr ';'                      { $$ = new ExprStmt((Expr)$1) { Line = @1.StartLine }; }
    | ';'                           { $$ = new ExprStmt(null) { Line = @1.StartLine }; }
    ;

selection_stmt
    : IF '(' expr ')' statement %prec IFX
                                    { $$ = new If((Expr)$3, $5, null) { Line = @1.StartLine }; }
    | IF '(' expr ')' statement ELSE statement
                                    { $$ = new If((Expr)$3, $5, $7) { Line = @1.StartLine }; }
    ;

iteration_stmt
    : WHILE '(' expr ')' statement  { $$ = new While((Expr)$3, $5) { Line = @1.StartLine }; }
    | DO statement WHILE '(' expr ')' ';'
                                    { $$ = new DoWhile($2, (Expr)$5) { Line = @1.StartLine }; }
    | FOR '(' for_init expr_opt ';' expr_opt ')' statement
                                    { $$ = new For($3, (Expr)$4, (Expr)$6, $8) { Line = @1.StartLine }; }
    ;

for_init
    : expr_opt ';'                  { $$ = $1; }
    | declaration                   { $$ = $1; }
    ;

expr_opt
    : expr                          { $$ = $1; }
    |                    { $$ = null; }
    ;

jump_stmt
    : RETURN expr ';'               { $$ = new Return((Expr)$2) { Line = @1.StartLine }; }
    | RETURN ';'                    { $$ = new Return(null) { Line = @1.StartLine }; }
    | BREAK ';'                     { $$ = new Break() { Line = @1.StartLine }; }
    | CONTINUE ';'                  { $$ = new Continue() { Line = @1.StartLine }; }
    ;

expr
    : expr '+' expr         { $$ = Bin("+", $1, $3, @2); }
    | expr '-' expr         { $$ = Bin("-", $1, $3, @2); }
    | expr '*' expr         { $$ = Bin("*", $1, $3, @2); }
    | expr '/' expr         { $$ = Bin("/", $1, $3, @2); }
    | expr '%' expr         { $$ = Bin("%", $1, $3, @2); }
    | expr '<' expr         { $$ = Bin("<", $1, $3, @2); }
    | expr '>' expr         { $$ = Bin(">", $1, $3, @2); }
    | expr LE expr          { $$ = Bin("<=", $1, $3, @2); }
    | expr GE expr          { $$ = Bin(">=", $1, $3, @2); }
    | expr EQ expr          { $$ = Bin("==", $1, $3, @2); }
    | expr NE expr          { $$ = Bin("!=", $1, $3, @2); }
    | expr AND expr         { $$ = Bin("&&", $1, $3, @2); }
    | expr OR expr          { $$ = Bin("||", $1, $3, @2); }
    | expr '&' expr         { $$ = Bin("&", $1, $3, @2); }
    | expr '|' expr         { $$ = Bin("|", $1, $3, @2); }
    | expr '^' expr         { $$ = Bin("^", $1, $3, @2); }
    | expr SHL expr         { $$ = Bin("<<", $1, $3, @2); }
    | expr SHR expr         { $$ = Bin(">>", $1, $3, @2); }

    | expr '=' expr         { $$ = Asg("=", $1, $3, @2); }
    | expr ADD_ASSIGN expr  { $$ = Asg("+=", $1, $3, @2); }
    | expr SUB_ASSIGN expr  { $$ = Asg("-=", $1, $3, @2); }
    | expr MUL_ASSIGN expr  { $$ = Asg("*=", $1, $3, @2); }
    | expr DIV_ASSIGN expr  { $$ = Asg("/=", $1, $3, @2); }
    | expr MOD_ASSIGN expr  { $$ = Asg("%=", $1, $3, @2); }
    | expr AND_ASSIGN expr  { $$ = Asg("&=", $1, $3, @2); }
    | expr OR_ASSIGN expr   { $$ = Asg("|=", $1, $3, @2); }
    | expr XOR_ASSIGN expr  { $$ = Asg("^=", $1, $3, @2); }
    | expr SHL_ASSIGN expr  { $$ = Asg("<<=", $1, $3, @2); }
    | expr SHR_ASSIGN expr  { $$ = Asg(">>=", $1, $3, @2); }

    | expr '?' expr ':' expr        { $$ = new Ternary((Expr)$1, (Expr)$3, (Expr)$5) { Line = @2.StartLine }; }

    | '-' expr %prec UNARY          { $$ = Un("-", $2, @1); }
    | '+' expr %prec UNARY          { $$ = Un("+", $2, @1); }
    | '!' expr %prec UNARY          { $$ = Un("!", $2, @1); }
    | '~' expr %prec UNARY          { $$ = Un("~", $2, @1); }
    | INC expr %prec UNARY          { $$ = Un("++", $2, @1); }
    | DEC expr %prec UNARY          { $$ = Un("--", $2, @1); }
    | expr INC                      { $$ = new UnaryOp("++", (Expr)$1, true) { Line = @2.StartLine }; }
    | expr DEC                      { $$ = new UnaryOp("--", (Expr)$1, true) { Line = @2.StartLine }; }
    | '(' type_spec ')' expr %prec UNARY
                                    { $$ = new Cast($2, (Expr)$4) { Line = @1.StartLine }; }

    | expr '[' expr ']'             { $$ = new Index((Expr)$1, (Expr)$3) { Line = @2.StartLine }; }
    | ID '(' arg_list ')'           { $$ = new Call($1, $3) { Line = @1.StartLine }; }
    | ID '(' ')'                    { $$ = new Call($1, new List<Node>()) { Line = @1.StartLine }; }
    | '(' expr ')'                  { $$ = $2; }

    | ID                            { $$ = new Ident($1) { Line = @1.StartLine }; }
    | INT_CONST                     { $$ = Const.Int($1, @1.StartLine); }
    | FLOAT_CONST                   { $$ = Const.Float($1, @1.StartLine); }
    | CHAR_CONST                    { $$ = Const.Char($1, @1.StartLine); }
    | string_list                   { $$ = Const.Str($1, @1.StartLine); }
    ;

string_list
    : string_list STRING            { $$ = $1 + $2; }
    | STRING                        { $$ = $1; }
    ;

arg_list
    : arg_list ',' expr             { $1.Add($3); $$ = $1; }
    | expr                          { $$ = new List<Node> { $1 }; }
    ;

%%
