// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Util
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class PreconditionsTest
    {
        [Fact]
        [Unit]
        public void TestCheckNotNull()
        {
            Assert.Throws<ArgumentNullException>(() => Preconditions.CheckNotNull<string>(null, "param"));
            Assert.Throws<ArgumentNullException>(() => Preconditions.CheckNotNull<string>(null, "param", "message"));
            Assert.Throws<ArgumentNullException>(() => Preconditions.CheckNotNull<string>(null));

            string message = Preconditions.CheckNotNull("my message");
            Assert.Equal("my message", message);

            message = Preconditions.CheckNotNull("my message", "param");
            Assert.Equal("my message", message);

            message = Preconditions.CheckNotNull("my message", "param", "message");
            Assert.Equal("my message", message);
        }

        [Fact]
        [Unit]
        public void TestCheckArgument()
        {
            Assert.Throws<ArgumentException>(() => Preconditions.CheckArgument(false));
            Assert.Throws<ArgumentException>(() => Preconditions.CheckArgument(false, "message"));
            Preconditions.CheckArgument(true);
        }

        [Fact]
        [Unit]
        public void TestCheckRange()
        {
            int item = Preconditions.CheckRange(6, 5);
            Assert.Equal(6, item);

            int item2 = Preconditions.CheckRange(6, 6, 7);
            Assert.Equal(6, item2);

            Assert.Throws<ArgumentOutOfRangeException>(() => Preconditions.CheckRange(5, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => Preconditions.CheckRange(5, 6, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => Preconditions.CheckRange(9, 6, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => Preconditions.CheckRange(8, 6, 8));
        }
    }
}
