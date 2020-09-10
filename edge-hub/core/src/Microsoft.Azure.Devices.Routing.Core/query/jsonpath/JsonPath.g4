grammar JsonPath;

/*
 Limited set of JsonPath notation. We support a subset of JsonPath spec.
 Check against spec description (http://goessner.net/articles/JsonPath/)
*/

jsonpath: expr
        ;

expr    : innerExpr ('.' innerExpr)* ;

innerExpr : IDENTIFIER
          | IDENTIFIER '[' INT ']'
          ;

INT                :   [0-9]+;
IDENTIFIER         :   ID_CHARS+ ;
fragment ID_CHARS  :   ~[()<>@,;:\\"/[\]?={}.\t\n\r];
WS                 :   [ \t\n\r]+ -> skip ;