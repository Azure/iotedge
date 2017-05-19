lexer grammar RouteLexer;

SELECT :  S E L E C T;

FROM :    F R O M;

WHERE :   W H E R E;

INTO :    I N T O;

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

fragment A
	:	'A' | 'a';

fragment B
	:	'B' | 'b';

fragment C
	:	'C' | 'c';

fragment D
	:	'D' | 'd';

fragment E
	:	'E' | 'e';

fragment F
	:	'F' | 'f';

fragment G
	:	'G' | 'g';

fragment H
	:	'H' | 'h';

fragment I
	:	'I' | 'i';

fragment J
	:	'J' | 'j';

fragment K
	:	'K' | 'k';

fragment L
	:	'L' | 'l';

fragment M
	:	'M' | 'm';

fragment N
	:	'N' | 'n';

fragment O
	:	'O' | 'o';

fragment P
	:	'P' | 'p';

fragment Q
	:	'Q' | 'q';

fragment R
	:	'R' | 'r';

fragment S
	:	'S' | 's';

fragment T
	:	'T' | 't';

fragment U
	:	'U' | 'u';

fragment V
	:	'V' | 'v';

fragment W
	:	'W' | 'w';

fragment X
	:	'X' | 'x';

fragment Y
	:	'Y' | 'y';

fragment Z
	:	'Z' | 'z';


