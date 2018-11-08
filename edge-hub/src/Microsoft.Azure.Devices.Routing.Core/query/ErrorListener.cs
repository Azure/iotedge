// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Antlr4.Runtime;

    using Microsoft.Azure.Devices.Routing.Core.Query.Errors;

    public class ErrorListener : BaseErrorListener
    {
        readonly List<CompilationError> errors = new List<CompilationError>();

        bool HasErrors => this.errors.Count > 0;

        /// <summary>
        /// Error for invalid arguments to built-in function
        /// </summary>
        /// <param name="token">Token</param>
        /// <param name="alternatives">Alternatives</param>
        /// <param name="given">Given</param>
        public void ArgumentError(IToken token, IList<IArgs> alternatives, Type[] given)
        {
            this.ArgumentError(token, alternatives, given, null);
        }

        /// <summary>
        /// Error for invalid arguments to built-in function
        /// </summary>
        /// <param name="token">Token</param>
        /// <param name="alternatives">Alternatives</param>
        /// <param name="given">Given</param>
        /// <param name="errorDetails">error details</param>
        public void ArgumentError(IToken token, IList<IArgs> alternatives, Type[] given, string errorDetails)
        {
            string arguments = string.Join(", ", given.Select(t => TypeName(t)));
            string message;

            if (string.IsNullOrWhiteSpace(errorDetails))
            {
                message = string.Format(CultureInfo.InvariantCulture, "Invalid arguments to built-in function '{0}': {0}({1})", token.Text, arguments);
            }
            else
            {
                message = string.Format(CultureInfo.InvariantCulture, "Invalid arguments to built-in function '{0}': {0}({1}). Error: {2}", token.Text, arguments, errorDetails);
            }

            this.Error(token, message);
        }

        /// <summary>
        /// Error for unrecognized built-in function
        /// </summary>
        /// <param name="token">Token</param>
        public void InvalidBuiltinError(IToken token)
        {
            this.Error(token, string.Format(CultureInfo.InvariantCulture, "Invalid built-in function '{0}'", token.Text));
        }

        /// <summary>
        /// Error for invalid operand(s) to operator
        /// </summary>
        /// <param name="token">Token</param>
        /// <param name="required">Required</param>
        /// <param name="given">Given</param>
        public void OperandError(IToken token, IArgs required, Type[] given)
        {
            string haveTypes = string.Join(" and ", given.Select(t => "'" + TypeName(t) + "'"));
            string expectedTypes = string.Join(" and ", required.Types.Select(t => "'" + TypeName(t) + "'"));
            string arity = required.Arity == 1 ? "unary " : required.Arity == 2 ? "binary " : string.Empty;
            this.Error(token, string.Format(CultureInfo.InvariantCulture, "Invalid operands to {0}operator '{1}': have {2}, expected {3}", arity, token.Text, haveTypes, expectedTypes));
        }

        /// <summary>
        /// Generic syntax error
        /// </summary>
        /// <param name="token">Token</param>
        /// <param name="message">Message</param>
        public void SyntaxError(IToken token, string message)
        {
            this.Error(token, string.Format(CultureInfo.InvariantCulture, "Syntax error: {0}", message));
        }

        /// <summary>
        /// Syntax error for parser
        /// </summary>
        /// <param name="recognizer">Recognizer</param>
        /// <param name="offendingSymbol">Offending Symbol</param>
        /// <param name="line">Line</param>
        /// <param name="charPositionInLine">Character position in line</param>
        /// <param name="msg">Message</param>
        /// <param name="e">Recognition exception</param>
        public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            this.Error(offendingSymbol, "Syntax error.");
        }

        /// <summary>
        /// Syntax error for unknown symbol
        /// </summary>
        /// <param name="token">Token</param>
        public void UnrecognizedSymbolError(IToken token)
        {
            this.SyntaxError(token, string.Format(CultureInfo.InvariantCulture, "invalid symbol '{0}'", token.Text));
        }

        /// <summary>
        /// Throws RouteCompilationException if errors were discovered while listening
        /// </summary>
        public void Validate()
        {
            if (this.HasErrors)
            {
                throw new RouteCompilationException(this.errors);
            }
        }

        static string TypeName(Type type)
        {
            return type == typeof(string) ? "string" :
                type == typeof(double) ? "number" :
                type == typeof(Bool) ? "bool" :
                type == typeof(Undefined) ? "undefined" :
                type == typeof(Null) ? "null" :
                "unknown";
        }

        void Error(IToken offendingSymbol, string message)
        {
            // A couple things to note about the start and end column range:
            //     1. line and column numbers are zero based from the lexer but one based for our API
            //     2. error ranges are start inclusive but end exclusive [start, end).
            string input = offendingSymbol.InputStream.ToString();
            string[] lines = input.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);

            int line = offendingSymbol.Line;
            int column = offendingSymbol.Column + 1;
            int tokenLength = offendingSymbol.Text.Length;
            int lineLength = lines[line - 1].Length;

            var start = new ErrorPosition(line, column);

            // Set the end column to the minimum of column + token length or line length + 2 (one based and exclusive end column, see note above)
            int endColumn = Math.Min(column + tokenLength, lineLength + 2);
            var end = new ErrorPosition(line, endColumn);
            var error = new CompilationError(ErrorSeverity.Error, message, new ErrorRange(start, end));
            this.errors.Add(error);
        }
    }
}
