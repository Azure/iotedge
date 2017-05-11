// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Query.JsonPath
{
    using System.Globalization;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Tree;
    using Microsoft.Azure.Devices.Routing.Core.Query.Errors;

    public static class JsonPathValidator
    {
        public static bool IsSupportedJsonPath(string jsonPath, out string errorDetails)
        {
            errorDetails = string.Empty;

            var errorListener = new ErrorListener();

            try
            {
                ParseJsonPath(jsonPath, errorListener);
                return true;
            }
            catch (RouteCompilationException ex)
            {
                foreach (CompilationError err in ex.Errors)
                {
                    errorDetails += string.Format(
                        CultureInfo.InvariantCulture,
                        "Message:{0}, Location <start>:{1}, <end>:{2}, Severity:{3} \n",
                        err.Message, err.Location?.Start, err.Location?.End, err.Severity);
                }

                return false;
            }
        }

        static void ParseJsonPath(string jsonPath, ErrorListener errorListener)
        {
            var input = new AntlrInputStream(jsonPath);

            var lexer = new JsonPathLexer(input);
            var tokens = new CommonTokenStream(lexer);
            var parser = new JsonPathParser(tokens);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            IParseTree tree = parser.jsonpath();

            errorListener.Validate();
        }
    }
}
