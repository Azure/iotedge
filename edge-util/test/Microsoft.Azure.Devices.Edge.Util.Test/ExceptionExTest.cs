// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class ExceptionExTest
    {
        public static IEnumerable<object[]> GetTestTimeoutExceptions()
        {
            yield return new object[] { new TimeoutException(), true };
            yield return new object[] { new InvalidOperationException("foo", new InvalidOperationException("bar", new TimeoutException())), true };
            yield return new object[] { new AggregateException(new InvalidOperationException(), new TimeoutException()), true };
            yield return new object[] { new AggregateException(new InvalidOperationException(), new InvalidOperationException("foo", new InvalidOperationException("bar", new TimeoutException()))), true };
            yield return new object[] { new InvalidOperationException(), false };
            yield return new object[] { new InvalidOperationException("foo", new InvalidOperationException("bar", new ArgumentException("abc"))), false };
            yield return new object[] { new AggregateException(new InvalidOperationException(), new ArgumentException()), false };
        }

        [Theory]
        [MemberData(nameof(GetTestTimeoutExceptions))]
        [Unit]
        public void TestHasTimeoutException(Exception ex, bool hasTimeoutException)
        {
            Assert.Equal(hasTimeoutException, ex.HasTimeoutException());
        }
    }
}
