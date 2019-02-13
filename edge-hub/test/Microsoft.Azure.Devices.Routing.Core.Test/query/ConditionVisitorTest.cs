// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Query
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Tree;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Query;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class ConditionVisitorTest : RoutingUnitTestBase
    {
        [Theory]
        [Unit]
        [InlineData("100", 100D)]
        [InlineData("0100", 100D)]
        [InlineData("(((3)))", 3D)]
        [InlineData("12.34", 12.34D)]
        [InlineData("1.234e1", 12.34D)]
        [InlineData("1.234e+1", 12.34D)]
        [InlineData("1.234e-1", 0.1234D)]
        [InlineData("0xDEADBEEF", 3735928559D)]
        [InlineData("0xDeadBeef", 3735928559D)]
        [InlineData("\"\" ", "")]
        [InlineData("\"Hello! \" ", "Hello! ")]
        [InlineData("'Hello! ' ", "Hello! ")]
        [InlineData(@"""he said, \""Hello!\""""", "he said, \"Hello!\"")]
        [InlineData(@"""he said, \""Tch\u00FCss!\""""", "he said, \"Tchüss!\"")]
        public void TestLiteral(string condition, object value)
        {
            Expression expression = ToExpression(condition);
            Assert.IsType<ConstantExpression>(expression);
            Assert.Equal(value, ((ConstantExpression)expression).Value);
        }

        [Theory]
        [Unit]
        [InlineData("3 + 4", 7)]
        [InlineData("3 + 4 * 5", 23)]
        [InlineData("3 + 4 / 4", 4)]
        [InlineData("3 - 4", -1)]
        [InlineData("3 - (4 + 0x10)", -17)]
        [InlineData("3 * 4", 12)]
        [InlineData("3 / 4", 0.75)]
        [InlineData("3 / 0", double.PositiveInfinity)]
        [InlineData("-3 / 0", double.NegativeInfinity)]
        [InlineData("3 % 4", 3.0)]
        [InlineData("3 % 0", double.NaN)]
        [InlineData("0 % 3", 0.0)]
        [InlineData("-1.234", -1.234)]
        [InlineData("-1.234 + 0.004", -1.23)]
        [InlineData("-(1.234 + 2)", -3.234)]
        [InlineData("1 - -2", 3)]
        public void TestArithmetic(string condition, double expected)
        {
            Expression expression = ToExpression(condition);
            Func<double> rule = Expression.Lambda<Func<double>>(expression).Compile();
            Assert.Equal(expected, rule());
        }

        [Theory]
        [Unit]
        [InlineData("\"unterminated = 'unterminated'", "Syntax error: unterminated string")]
        [InlineData("@32 = 32", "Syntax error: invalid symbol '@'")]
        [InlineData("32 = [32]", "Syntax error.")]
        [InlineData("(@)", "Syntax error: invalid symbol '@'")]
        public void TestLiteralErrors(string condition, string message)
        {
            var exception = Assert.Throws<RouteCompilationException>(() => ToExpression(condition));
            Assert.Equal(message, exception.Errors.First().Message);
        }

        static Expression ToExpression(string condition)
        {
            var errorListener = new ErrorListener();
            var input = new AntlrInputStream(condition);
            var lexer = new ConditionLexer(input);
            var tokens = new CommonTokenStream(lexer);
            var parser = new ConditionParser(tokens);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            IParseTree tree = parser.condition();
            errorListener.Validate();

            ParameterExpression parameter = Expression.Parameter(typeof(IMessage), "message");

            var testRoute = new Route(
                Guid.NewGuid().ToString(),
                "true",
                nameof(ConditionVisitorTest),
                TelemetryMessageSource.Instance,
                new HashSet<Endpoint>());

            var visitor = new ConditionVisitor(parameter, errorListener, testRoute, RouteCompilerFlags.All);

            Expression result = visitor.Visit(tree);
            errorListener.Validate();
            return result;
        }
    }
}
