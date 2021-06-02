grammar Route;
import Condition;

/*
 * Parser Rules
 */

 route
	:	SELECT '*' FROM source ( WHERE routecondition )? INTO sink
	|	FROM source ( WHERE routecondition )? INTO sink
	|	EOF
	;

 routecondition
	:	condition
	;

 source
	:	path
	;

 sink
	:	SYS_PROP                                        # SystemEndpoint
	|	func=ID '(' endpoint=STRING ')'                 # FuncEndpoint
	;

 path
	:	'/' item path
	|	'/' item
	;

 item
	:	'*' 
	|	ID
	;

/*
 * Lexer Rules - Are defined in RouteLexer.g4
 */
