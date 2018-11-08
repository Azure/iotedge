// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Query.Errors
{
    using System.Diagnostics.CodeAnalysis;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Query.Errors;

    using Xunit;

    [ExcludeFromCodeCoverage]
    public class ErrorRangeTest : RoutingUnitTestBase
    {
        [Fact]
        [Unit]
        public void TestEquals()
        {
            var range1 = new ErrorRange(new ErrorPosition(1, 1), new ErrorPosition(1, 3));
            var range2 = new ErrorRange(new ErrorPosition(1, 1), new ErrorPosition(1, 3));
            var range3 = new ErrorRange(new ErrorPosition(1, 1), new ErrorPosition(1, 4));
            var range4 = new ErrorRange(new ErrorPosition(1, 2), new ErrorPosition(1, 3));

            Assert.Equal(range1, range2);
            Assert.NotEqual(range1, range3);
            Assert.NotEqual(range1, range4);

            // ReSharper disable once EqualExpressionComparison
            Assert.True(range1.Equals(range1));
            Assert.False(range1.Equals(null));
            Assert.False(range1.Equals(new object()));
        }
    }
}
