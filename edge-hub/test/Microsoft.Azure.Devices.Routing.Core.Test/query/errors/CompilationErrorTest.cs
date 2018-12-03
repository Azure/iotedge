// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Test.Query.Errors
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Routing.Core.Query.Errors;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class CompilationErrorTest : RoutingUnitTestBase
    {
        [Fact, Unit]
        public void SmokeTest()
        {
            var start = new ErrorPosition(2, 2);
            var end = new ErrorPosition(2, 3);
            var range = new ErrorRange(start, end);
            var error = new CompilationError(ErrorSeverity.Warning, "message", range);

            Assert.Equal("message", error.Message);
            Assert.Equal(ErrorSeverity.Warning, error.Severity);
            Assert.Equal(new ErrorPosition(2, 2), error.Location.Start);
            Assert.Equal(new ErrorPosition(2, 3), error.Location.End);

            Assert.Throws<ArgumentException>(() => new CompilationError(ErrorSeverity.Error, "message", new ErrorRange(end, start)));
        }

        [Fact, Unit]
        public void TestEquals()
        {
            var error1 = new CompilationError(ErrorSeverity.Error, "message", new ErrorRange(new ErrorPosition(1, 1), new ErrorPosition(1, 2)));
            var error2 = new CompilationError(ErrorSeverity.Error, "message", new ErrorRange(new ErrorPosition(1, 1), new ErrorPosition(1, 2)));
            var error3 = new CompilationError(ErrorSeverity.Warning, "message", new ErrorRange(new ErrorPosition(1, 1), new ErrorPosition(1, 2)));
            var error4 = new CompilationError(ErrorSeverity.Error, "different", new ErrorRange(new ErrorPosition(1, 1), new ErrorPosition(1, 2)));
            var error5 = new CompilationError(ErrorSeverity.Error, "message", new ErrorRange(new ErrorPosition(1, 2), new ErrorPosition(1, 3)));

            Assert.Equal(error1, error2);
            Assert.NotEqual(error1, error3);
            Assert.NotEqual(error1, error4);
            Assert.NotEqual(error1, error5);
            Assert.Equal(error1.GetHashCode(), error2.GetHashCode());
            Assert.NotEqual(error1.GetHashCode(), error3.GetHashCode());

            // ReSharper disable once EqualExpressionComparison
            Assert.True(error1.Equals(error1));
            Assert.False(error1.Equals(null));
            Assert.False(error1.Equals(new object()));
        }
    }
}
