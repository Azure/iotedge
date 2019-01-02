// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Query.Errors
{
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Routing.Core.Query.Errors;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class ErrorPositionTest : RoutingUnitTestBase
    {
        [Fact, Unit]
        public void TestEquals()
        {
            var pos1 = new ErrorPosition(1, 1);
            var pos2 = new ErrorPosition(1, 2);
            var pos3 = new ErrorPosition(2, 1);
            var pos4 = new ErrorPosition(1, 1);

            Assert.Equal(pos1, pos4);
            Assert.NotEqual(pos1, pos2);
            Assert.NotEqual(pos1, pos3);

            // ReSharper disable once EqualExpressionComparison
            Assert.True(pos1.Equals(pos1));
            Assert.True(pos1.Equals(pos4));
            Assert.False(pos1.Equals(null));
            Assert.False(pos1.Equals(new object()));

            Assert.Equal(pos1.GetHashCode(), pos4.GetHashCode());
            Assert.NotEqual(pos1.GetHashCode(), pos2.GetHashCode());
        }

        [Fact, Unit]
        public void TestCompareTo()
        {
            var pos1 = new ErrorPosition(1, 1);
            var pos2 = new ErrorPosition(1, 2);
            var pos3 = new ErrorPosition(2, 1);
            var pos4 = new ErrorPosition(1, 1);

            Assert.True(pos1 < pos2);
            Assert.False(pos1 < pos4);
            Assert.True(pos1 <= pos2);
            Assert.True(pos1 <= pos4);
            Assert.True(pos2 > pos1);
            Assert.False(pos1 > pos4);
            Assert.True(pos2 >= pos1);
            Assert.True(pos1 >= pos4);
            Assert.True(pos1 == pos4);
            Assert.True(pos1 != pos2);

            Assert.True(pos3 > pos1);
            Assert.True(pos3 > pos2);
        }
    }
}
