// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class SaslIdentityTest
    {
        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidParseInputs))]
        public void TestInvalidParseInputs(string input, Type exceptionType) =>
            Assert.Throws(exceptionType, () => SaslIdentity.Parse(input));

        public static IEnumerable<object[]> GetInvalidParseInputs() => new[]
        {
            new object[] { string.Empty, typeof(ArgumentException) },
            new object[] { "    ", typeof(ArgumentException) },
            new object[] { "boo", typeof(EdgeHubConnectionException) },
        };
    }
}
