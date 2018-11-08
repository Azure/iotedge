// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using System;
    using System.Linq.Expressions;

    using Antlr4.Runtime;
    using Antlr4.Runtime.Tree;

    public class RouteCompiler : IRouteCompiler
    {
        public static RouteCompiler Instance { get; } = new RouteCompiler();

        public Func<IMessage, Bool> Compile(Route route)
        {
            return this.Compile(route, RouteCompilerFlags.None);
        }

        public Func<IMessage, Bool> Compile(Route route, RouteCompilerFlags routeCompilerFlags)
        {
            var errorListener = new ErrorListener();
            IParseTree tree = GetParseTree(route, errorListener);
            errorListener.Validate();

            ParameterExpression parameter = Expression.Parameter(typeof(IMessage), "message");
            var visitor = new ConditionVisitor(parameter, errorListener, route, routeCompilerFlags);
            Expression expression = visitor.Visit(tree);
            errorListener.Validate();

            Func<IMessage, Bool> rule = Expression.Lambda<Func<IMessage, Bool>>(Expression.Convert(expression, typeof(Bool)), parameter).Compile();
            return rule;
        }

        public int GetComplexity(Route route)
        {
            var errorListener = new ErrorListener();
            IParseTree tree = GetParseTree(route, errorListener);
            errorListener.Validate();

            return GetComplexityRecursively(tree);
        }

        static int GetComplexityRecursively(ITree root)
        {
            int complexity = root.ChildCount == 0 ? 1 : 0;

            for (int i = 0; i < root.ChildCount; i++)
            {
                complexity += GetComplexityRecursively(root.GetChild(i));
            }

            return complexity;
        }

        static IParseTree GetParseTree(Route route, ErrorListener errorListener)
        {
            var input = new AntlrInputStream(route.Condition);
            var lexer = new ConditionLexer(input);
            var tokens = new CommonTokenStream(lexer);
            var parser = new ConditionParser(tokens);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            return parser.condition();
        }
    }
}
