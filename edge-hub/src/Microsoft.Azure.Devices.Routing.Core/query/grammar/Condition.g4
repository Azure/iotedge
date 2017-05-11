grammar Condition;

/*
 * Parser Rules
 */

condition
    :   expr                                                # Expression
    |   EOF                                                 # Eof
    ;

expr
    :   nested_expr                                         # Nested
    |   literal                                             # Lit
    |   SYS_PROP                                            # SysProperty
    |   ID                                                  # Property
    |   '{' prop=SYS_PROP '}'                               # SysPropertyEscaped
    |   fcall                                               # Func
    |   op='-' expr                                         # Negate
    |   op=NOT expr                                         # Not
    |   left=expr op=('*' | '/'  | '%'       ) right=expr   # MulDivMod
    |   left=expr op=('+' | '-'  | '||'      ) right=expr   # AddSubConcat
    |   left=expr op=('<' | '<=' | '>' | '>=') right=expr   # Compare
    |   left=expr op=('=' | '!=' | '<>'      ) right=expr   # Equality
    |   left=expr op=AND right=expr                         # And
    |   left=expr op=OR right=expr                          # Or
    |   left=expr op='??' right=expr                        # Coalesce

		// Syntax Errors
    |   token=syntax_error                                  # SyntaxError
    |   token=syntax_error expr                             # SyntaxErrorUnaryOp
    |   expr token=syntax_error expr                        # SyntaxErrorBinaryOp
    |   nested_expr paren=')'                               # SyntaxErrorExtraParens
    |   fcall paren=')'                                     # SyntaxErrorExtraParensFunc
    |   paren='(' expr                                      # SyntaxErrorMissingParen
    ;

exprList : expr (',' expr)* ;

fcall
    :   func=ID '(' exprList? ')'
    ;

nested_expr
    :   '(' expr ')'
    ;

literal
    : (TRUE | FALSE)            # Bool
    | STRING                    # String
    | UNTERMINATED_STRING       # UnterminatedString
    | INTEGER_LITERAL           # Integer
    | HEX_INTEGER_LITERAL       # Hex
    | REAL_LITERAL              # Real
    | NULL                      # Null
    | UNDEFINED                 # Undefined
    ;

syntax_error
    : UNKNOWN_CHAR
    ;

/*
 * Lexer Rules
 */

// string
STRING
    :   '"' (ESC | ~["\\])* '"'
    |   '\'' (ESC | ~['\\])* '\''
    ;
UNTERMINATED_STRING
    :   '"' (ESC | ~["\\])*
    |   '\'' (ESC | ~['\\])*
    ;
fragment ESC        : '\\' (["'\\] | UNICODE) ;
fragment UNICODE    : 'u' HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT ;
fragment HEX_DIGIT  : [0-9a-fA-F];

// Numbers
INTEGER_LITERAL
    :   '-'? INT
    ;
HEX_INTEGER_LITERAL
    :   '0' [Xx] HEX_DIGIT+
    ;
REAL_LITERAL
    :   '-'? [0-9]* '.' [0-9]+ EXP?
    |   '-'? [0-9]* EXP;

fragment INT :  [0-9]+;
fragment EXP :  [Ee] [+\-]? INT;

TRUE :          'true' ;
FALSE :         'false' ;
NULL :          'null' ;
UNDEFINED :     'undefined';

OPEN_PARENS:    '(' ;
CLOSE_PARENS:   ')' ;
COMMA:          ',' ;
PLUS:           '+' ;
MINUS:          '-' ;
OP_MUL:         '*' ;
OP_DIV:         '/' ;
OP_MOD:         '%' ;
AND:            [Aa] [Nn] [Dd] ;
OR:             [Oo] [Rr] ;
NOT:            [Nn] [Oo] [Tt] ;
OP_EQ:          '=' ;
OP_NE1:         '!=' ;
OP_NE2:         '<>' ;
OP_LE:          '<=' ;
OP_GE:          '>=' ;
OP_GT:          '>' ;
OP_LT:          '<' ;
OP_CONCAT:      '||' ;
OP_COALESCE:    '??' ;

SYS_PROP    :  '$' QUERY_PATH ;

ID          :   ID_CHARS+ ;
QUERY_PATH   :   QUERY_PATH_CHARS+ ;

fragment QUERY_PATH_CHARS : ~[()<>@,;:\\"/?={} \t\n\r];
fragment ID_CHARS :   ~[()<>@,;:\\"/[\]?={} \t\n\r];

WS:         [ \t\r\n]+ -> skip;
COMMENT:    '/*' .*? '*/' -> channel(HIDDEN);

// catches all remaining characters as an error token, moving all error handling from the lexer to the parser
UNKNOWN_CHAR: . ;