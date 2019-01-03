// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Query
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Microsoft.Azure.Devices.Routing.Core.Query;
    using Microsoft.Azure.Devices.Routing.Core.Query.Errors;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class ErrorListenerTest : RoutingUnitTestBase
    {
        [Fact, Unit]
        public void TestOperandError()
        {
            string condition = "3 + '4' = 7";
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            var exception = Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route));

            Assert.Equal(2, exception.Errors.Count);

            CompilationError error1 = exception.Errors.First();
            Assert.Equal("Invalid operands to binary operator '+': have 'number' and 'string', expected 'number' and 'number'", error1.Message);
            Assert.Equal(new ErrorRange(new ErrorPosition(1, 3), new ErrorPosition(1, 4)), error1.Location);
            Assert.Equal(ErrorSeverity.Error, error1.Severity);
        }

        [Fact, Unit]
        public void TestArgumentError()
        {
            string condition = "as_number(true) = true";
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            var exception = Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route));

            Assert.Equal(2, exception.Errors.Count);

            CompilationError error1 = exception.Errors.First();
            Assert.Equal("Invalid arguments to built-in function 'as_number': as_number(bool)", error1.Message);
            Assert.Equal(new ErrorRange(new ErrorPosition(1, 1), new ErrorPosition(1, 10)), error1.Location);
            Assert.Equal(ErrorSeverity.Error, error1.Severity);
        }

        [Fact, Unit]
        public void TestInvalidBuiltinError()
        {
            string condition = "nope(true) = true";
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            var exception = Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route));

            Assert.Equal(2, exception.Errors.Count);

            CompilationError error1 = exception.Errors.First();
            Assert.Equal("Invalid built-in function 'nope'", error1.Message);
            Assert.Equal(new ErrorRange(new ErrorPosition(1, 1), new ErrorPosition(1, 5)), error1.Location);
            Assert.Equal(ErrorSeverity.Error, error1.Severity);
        }

        [Fact, Unit]
        public void TestSyntaxErrorMissingParens()
        {
            string condition = "(2 + 22 = 24";
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            var exception = Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route));

            Assert.Equal(1, exception.Errors.Count);

            CompilationError error1 = exception.Errors.First();
            Assert.Equal("Syntax error: missing closing ')'", error1.Message);
            Assert.Equal(new ErrorRange(new ErrorPosition(1, 1), new ErrorPosition(1, 2)), error1.Location);
            Assert.Equal(ErrorSeverity.Error, error1.Severity);
        }

        [Fact, Unit]
        public void TestSyntaxErrorMissingParensFunc()
        {
            string condition = "as_number(\"2\" = 24";
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            var exception = Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route));

            Assert.Equal(1, exception.Errors.Count);

            CompilationError error1 = exception.Errors.First();
            Assert.Equal("Syntax error.", error1.Message);
            Assert.Equal(new ErrorRange(new ErrorPosition(1, 19), new ErrorPosition(1, 20)), error1.Location);
            Assert.Equal(ErrorSeverity.Error, error1.Severity);
        }

        [Fact, Unit]
        public void TestSyntaxErrorExtraParens()
        {
            string condition = "(2 + 22 )) = 24";
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            var exception = Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route));

            Assert.Equal(2, exception.Errors.Count);

            CompilationError error1 = exception.Errors.First();
            Assert.Equal("Syntax error: unmatched closing ')'", error1.Message);
            Assert.Equal(new ErrorRange(new ErrorPosition(1, 10), new ErrorPosition(1, 11)), error1.Location);
            Assert.Equal(ErrorSeverity.Error, error1.Severity);
        }

        [Fact, Unit]
        public void TestSyntaxErrorExtraParensFunc()
        {
            string condition = "as_number(\"2\")) = 24";
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            var exception = Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route));

            Assert.Equal(2, exception.Errors.Count);

            CompilationError error1 = exception.Errors.First();
            Assert.Equal("Syntax error: unmatched closing ')'", error1.Message);
            Assert.Equal(new ErrorRange(new ErrorPosition(1, 15), new ErrorPosition(1, 16)), error1.Location);
            Assert.Equal(ErrorSeverity.Error, error1.Severity);
        }

        [Fact, Unit]
        public void TestSyntaxErrorUnterminatedString()
        {
            string condition = "\"2 = 24";
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            var exception = Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route));

            Assert.Equal(1, exception.Errors.Count);

            CompilationError error1 = exception.Errors.First();
            Assert.Equal("Syntax error: unterminated string", error1.Message);
            Assert.Equal(new ErrorRange(new ErrorPosition(1, 1), new ErrorPosition(1, 8)), error1.Location);
            Assert.Equal(ErrorSeverity.Error, error1.Severity);
        }

        [Fact, Unit]
        public void TestSyntaxErrorUnrecognizedSymbol()
        {
            string condition = "3 = @ 3";
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            var exception = Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route));

            Assert.Equal(2, exception.Errors.Count);

            CompilationError error1 = exception.Errors.First();
            Assert.Equal("Syntax error: invalid symbol '@'", error1.Message);
            Assert.Equal(new ErrorRange(new ErrorPosition(1, 5), new ErrorPosition(1, 6)), error1.Location);
            Assert.Equal(ErrorSeverity.Error, error1.Severity);
        }
    }
}
