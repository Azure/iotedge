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

        [Theory]
        [Unit]
        [MemberData(nameof(GetValidParseInputs))]
        public void TestValidParseInputs(string input) => Assert.NotNull(SaslIdentity.Parse(input));

        static IEnumerable<object[]> GetInvalidParseInputs() => new[]
        {
            new object[] { string.Empty, typeof(ArgumentException) },
            new object[] { "    ", typeof(ArgumentException) },
            new object[] { "boo", typeof(EdgeHubConnectionException) },
        };

        static IEnumerable<object[]> GetValidParseInputs() => new[]
        {
            // DeviceSasIdentityRegex
            new object[] { "dev1/modules/mod1@sas.hub1" },
            new object[] { "dev1@sas.hub1" },
            // should the following expression pass?! we're using an arbitrary symbol after the "@sas" token
            new object[] { "dev1/modules/mod1@sasXhub1" },

            // DeviceUserPasswordIdentityRegex
            new object[] { "dev1/modules/mod1@boo" },
            new object[] { "dev1@boo" }
        };
    }
}
