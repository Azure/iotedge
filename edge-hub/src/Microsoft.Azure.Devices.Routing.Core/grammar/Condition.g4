grammar Condition;
import RouteLexer;

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
 * Lexer Rules - Are defined in RouteLexer.g4
 */

