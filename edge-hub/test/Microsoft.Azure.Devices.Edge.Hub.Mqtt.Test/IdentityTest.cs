// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using System.Security.Authentication;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class IdentityTest
    {
        static readonly string SasToken = TokenHelper.CreateSasToken("TestHub.azure-devices.net/devices/device_2");

        static IEnumerable<object[]> GetConnectionStringInputs()
        {
            var connStrParts = new Dictionary<string, string>
            {
                { "HostName", "TestHub.azure-devices.net" },
                { "DeviceId", "device_2" },
                { "SharedAccessSignature", $"{SasToken}" },
                { "X509Cert", "False" },
            };

            var connStrBuilder = new StringBuilder();
            foreach (KeyValuePair<string, string> part in connStrParts)
            {
                if (connStrBuilder.Length > 0)
                {
                    connStrBuilder.Append(";");
                }
                connStrBuilder.Append($"{part.Key}={part.Value}");
            }

            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "device_2",
                AuthenticationScope.SasToken,
                null,
                SasToken,
                connStrBuilder.ToString()
            };
        }

        static IEnumerable<object[]> GetIdentityInputs()
        {
            yield return new object[]
            {
                "TestHub.azure-devices.net/Device_2/api-version=2016-11-14/DeviceClientType=Microsoft.Azure.Devices.Client/1.2.2",
                "TestHub.azure-devices.net",
                SasToken,
                true,
                typeof(DeviceIdentity)
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net/Device_2/Module_1/api-version=2016-11-14/DeviceClientType=Microsoft.Azure.Devices.Client/1.2.2",
                "TestHub.azure-devices.net",
                SasToken,
                true,
                typeof(ModuleIdentity)
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "TestHub.azure-devices.net",
                SasToken,
                false,
                typeof(InvalidCredentialException)
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net/Device_2/api-version=2016-11-14/DeviceClientType=Microsoft.Azure.Devices.Client/1.2.2",
                "TestHub.azure-devices.net",
                SasToken.Substring(0, SasToken.Length - 20),
                false,
                typeof(FormatException)
            };
        }

        static IEnumerable<object[]> GetModuleIdentityInputs()
        {
            yield return new object[]
            {
                "TestHub.azure-devices.net/Device_2/module_1232/api-version=2016-11-14/DeviceClientType=Microsoft.Azure.Devices.Client/1.2.2",
                "TestHub.azure-devices.net",
                SasToken,
                "Device_2",
                "module_1232"
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net/Device_2/Module_1/api-version=2016-11-14/DeviceClientType=Microsoft.Azure.Devices.Client/1.2.2",
                "TestHub.azure-devices.net",
                SasToken,
                "Device_2",
                "Module_1"
            };
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetIdentityInputs))]
        public void GetIdentityTest(string value,
            string iotHubHostName,
            string token,
            bool success,
            Type expectedType)
        {
            IIdentityFactory factory = new IdentityFactory(iotHubHostName);
            Try<Identity> identity = factory.GetWithSasToken(value, token);
            Assert.NotNull(identity);
            Assert.Equal(success, identity.Success);
            if (identity.Success)
            {
                Assert.NotNull(identity.Value);
                Assert.IsType(expectedType, identity.Value);
                Assert.Equal(iotHubHostName, identity.Value.IotHubHostName);
            }
            else
            {
                Assert.IsType(expectedType, identity.Exception);
            }
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetModuleIdentityInputs))]
        public void GetModuleIdentityTest(string value,
            string iotHubHostName,
            string token,
            string deviceId,
            string moduleId)
        {
            IIdentityFactory factory = new IdentityFactory(iotHubHostName);
            Try<Identity> identity = factory.GetWithSasToken(value, token);
            Assert.NotNull(identity);
            Assert.Equal(true, identity.Success);
            Assert.NotNull(identity.Value);
            var hubModuleIdentity = identity.Value as IModuleIdentity;
            Assert.NotNull(hubModuleIdentity);
            Assert.Equal(deviceId, hubModuleIdentity.DeviceId);
            Assert.Equal(moduleId, hubModuleIdentity.ModuleId);
            Assert.Equal($"{deviceId}/{moduleId}", hubModuleIdentity.Id);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetConnectionStringInputs))]
        public void GetConnectionStringTest(string iotHubHostName,
            string deviceId,
            AuthenticationScope scope,
            string policyName,
            string secret,
            string expectedConnectionString)
        {
            string connectionString = IdentityFactory.GetConnectionString(iotHubHostName, deviceId, AuthenticationScope.SasToken, policyName, secret);
            Assert.Equal(expectedConnectionString, connectionString);
        }
    }
}