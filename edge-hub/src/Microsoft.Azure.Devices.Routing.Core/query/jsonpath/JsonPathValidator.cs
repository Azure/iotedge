// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.JsonPath
{
    using Antlr4.Runtime;

    using Microsoft.Azure.Devices.Routing.Core.Query.Errors;

    using static System.FormattableString;

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
                    errorDetails += Invariant($"Message:{err.Message}, Location: ({err.Location.Start}, {err.Location.End}), Severity:{err.Severity} \n");
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

            parser.jsonpath();
            errorListener.Validate();
        }
    }
}
