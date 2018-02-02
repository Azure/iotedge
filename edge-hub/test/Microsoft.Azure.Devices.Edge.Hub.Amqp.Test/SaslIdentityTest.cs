// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp;
    using Xunit;
    using Microsoft.Azure.Devices.Edge.Util;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public class SaslIdentityTest
    {
        [Fact]
        [Unit]
        public void TestInvalidConstructorInputs()
        {
            string connectionString = "cs";
            Option<string> deviceId = Option.Some("device1");
            Option<string> moduleId = Option.Some("mod1");
            string key = "key1";
            string hubName = "hub1";
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaslIdentity((SaslIdentityType)10, connectionString, key, deviceId, moduleId, hubName));
            Assert.Throws<ArgumentException>(() => new SaslIdentity(SaslIdentityType.SharedAccessSignature, null, key, deviceId, moduleId, hubName));
            Assert.Throws<ArgumentException>(() => new SaslIdentity(SaslIdentityType.SharedAccessSignature, "   ", key, deviceId, moduleId, hubName));
            Assert.Throws<ArgumentException>(() => new SaslIdentity(SaslIdentityType.SharedAccessSignature, connectionString, null, deviceId, moduleId, hubName));
            Assert.Throws<ArgumentException>(() => new SaslIdentity(SaslIdentityType.SharedAccessSignature, connectionString, "   ", deviceId, moduleId, hubName));
            Assert.Throws<ArgumentException>(() => new SaslIdentity(SaslIdentityType.SharedAccessSignature, connectionString, key, deviceId, moduleId, null));
            Assert.Throws<ArgumentException>(() => new SaslIdentity(SaslIdentityType.SharedAccessSignature, connectionString, key, deviceId, moduleId, "    "));
            Assert.True(new SaslIdentity(SaslIdentityType.SharedAccessSignature, connectionString, key, deviceId, moduleId, hubName) != null);
        }

        static IEnumerable<object[]> GetInvalidParseInputs() => new[]
        {
                new object[] { string.Empty, typeof(ArgumentException) },
                new object[] { "    ", typeof(ArgumentException) },
                new object[] { "boo", typeof(EdgeHubConnectionException) },
            };

        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidParseInputs))]
        public void TestInvalidParseInputs(string input, Type exceptionType) =>
            Assert.Throws(exceptionType, () => SaslIdentity.Parse(input));

        static IEnumerable<object[]> GetValidParseInputs() => new[]
        {
                // IotHubSasIdentityRegex
                new object[] { "key1@sas.root.hub1" },
                new object[] { "kEY123-aa@sas.root.hub1" },
                new object[] { "____----___@sas.root.hub1" },

                // DeviceSasIdentityRegex
                new object[] { "dev1/modules/mod1@sas.hub1" },
                new object[] { "dev1@sas.hub1" },
                // should the following expression pass?! we're using an arbitrary symbol after the "@sas" token
                new object[] { "dev1/modules/mod1@sasXhub1" },

                // DeviceUserPasswordIdentityRegex
                new object[] { "dev1/modules/mod1@boo" },
                new object[] { "dev1@boo" }
            };

        [Theory]
        [Unit]
        [MemberData(nameof(GetValidParseInputs))]
        public void TestValidParseInputs(string input) => Assert.NotNull(SaslIdentity.Parse(input));
    }
}
