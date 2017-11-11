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
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public class IdentityTest
    {
        static readonly string Hostname = "TestHub.azure-devices.net";
        static readonly string DeviceId = "device_2";
        static readonly string ModuleId = "Module_1";
        static readonly string SasToken = TokenHelper.CreateSasToken($"{Hostname}/devices/{DeviceId}");
        static readonly string ApiVersion = "api-version=2016-11-14";
        static readonly string ProductInfo = "don't care";
        static readonly string DeviceClientType = $"DeviceClientType={ProductInfo}";

        static IEnumerable<object[]> GetConnectionStringInputs()
        {
            var connStrParts = new Dictionary<string, string>
            {
                { "HostName", Hostname },
                { "DeviceId", DeviceId },
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
                Hostname,
                DeviceId,
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
                $"{Hostname}/{DeviceId}/{ApiVersion}&{DeviceClientType}",
                Hostname,
                SasToken,
                true,
                typeof(DeviceIdentity)
            };

            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/{ModuleId}/{ApiVersion}&{DeviceClientType}",
                Hostname,
                SasToken,
                true,
                typeof(ModuleIdentity)
            };

            yield return new object[]
            {
                Hostname,
                Hostname,
                SasToken,
                false,
                typeof(EdgeHubConnectionException)
            };

            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/{ApiVersion}&{DeviceClientType}",
                Hostname,
                SasToken.Substring(0, SasToken.Length - 20),
                false,
                typeof(FormatException)
            };
        }

        static IEnumerable<object[]> GetModuleIdentityInputs()
        {
            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/module_1232/{ApiVersion}&{DeviceClientType}",
                Hostname,
                SasToken,
                DeviceId,
                "module_1232"
            };

            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/{ModuleId}/{ApiVersion}&{DeviceClientType}",
                Hostname,
                SasToken,
                DeviceId,
                ModuleId
            };
        }

        static IEnumerable<string[]> GetIdentityWithProductInfoInputs()
        {
            yield return new string[]
            {   // happy path
                "abc",
                $"{Hostname}/{DeviceId}/{ApiVersion}&{DeviceClientType}",
                $"abc {ProductInfo}"
            };

            yield return new string[]
            {   // no DeviceClientType
                "abc",
                $"{Hostname}/{DeviceId}/{ApiVersion}",
                "abc"
            };

            yield return new string[]
            {   // no caller product info
                string.Empty,
                $"{Hostname}/{DeviceId}/{ApiVersion}&{DeviceClientType}",
                ProductInfo
            };

            yield return new string[]
            {   // no DeviceClientType OR caller product info
                string.Empty,
                $"{Hostname}/{DeviceId}/{ApiVersion}",
                string.Empty
            };
        }

        static IEnumerable<string[]> GetUsernameInputs()
        {
            string devicePrefix = $"{Hostname}/{DeviceId}/{ApiVersion}";
            string modulePrefix = $"{Hostname}/{DeviceId}/{ModuleId}/{ApiVersion}";

            yield return new string[]
            {
                $"{devicePrefix}",
                string.Empty
            };

            yield return new string[]
            {
                $"{modulePrefix}",
                string.Empty
            };

            yield return new string[]
            {
                $"{devicePrefix}&DeviceClientType=",
                string.Empty
            };

            yield return new string[]
            {
                $"{modulePrefix}&DeviceClientType=",
                string.Empty
            };

            yield return new string[]
            {
                $"{devicePrefix}&{DeviceClientType}",
                ProductInfo
            };

            yield return new string[]
            {
                $"{modulePrefix}&{DeviceClientType}",
                ProductInfo
            };

            yield return new string[]
            {
                $"{Hostname}/{DeviceId}/{DeviceClientType}&{ApiVersion}",
                ProductInfo
            };

            yield return new string[]
            {
                $"{Hostname}/{DeviceId}/{ModuleId}/{DeviceClientType}&{ApiVersion}",
                ProductInfo
            };

            yield return new string[]
            {
                $"{devicePrefix}&{DeviceClientType}&DeviceClientType=abc123",
                ProductInfo
            };

            yield return new string[]
            {
                $"{devicePrefix}&{DeviceClientType}=abc123",
                $"{ProductInfo}=abc123"
            };
        }

        static IEnumerable<string[]> GetBadUsernameInputs()
        {
            yield return new string[] { "missingEverythingAfterHostname" };
            yield return new string[] { "hostname/missingEverthingAfterDeviceId" };
            yield return new string[] { "hostname/deviceId/missingApiVersionProperty" };
            yield return new string[] { "hostname/deviceId/moduleId/missingApiVersionProperty" };
            yield return new string[] { "hostname/deviceId/moduleId/stillMissingApiVersionProperty&DeviceClientType=whatever" };
            yield return new string[] { "hostname/deviceId/moduleId/DeviceClientType=whatever&stillMissingApiVersionProperty" };
            yield return new string[] { "hostname/deviceId/moduleId/DeviceClientType=stillMissingApiVersionProperty" };
            yield return new string[] { "hostname/deviceId/moduleId/api-version=whatever/tooManySegments" };
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
            Try<IIdentity> identity = factory.GetWithSasToken(value, token);
            Assert.NotNull(identity);
            Assert.Equal(success, identity.Success);
            if (identity.Success)
            {
                Assert.NotNull(identity.Value);
                Assert.IsType(expectedType, identity.Value);
                Assert.Equal(iotHubHostName, (identity.Value as Identity).IotHubHostName);
                Assert.Equal(ProductInfo, identity.Value.ProductInfo);
            }
            else
            {
                Assert.IsType(expectedType, identity.Exception);
            }
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetIdentityWithProductInfoInputs))]
        public void GetIdentityWithProductInfoTest(string productInfo, string username, string result)
        {
            IIdentityFactory factory = new IdentityFactory(Hostname, productInfo);
            Try<IIdentity> identity = factory.GetWithSasToken(username, SasToken);
            Assert.Equal(result, identity.Value.ProductInfo);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetUsernameInputs))]
        public void ProductInfoTest(string username, string productInfo)
        {
            IIdentityFactory factory = new IdentityFactory(Hostname);
            var identity = factory.GetWithSasToken(username, SasToken).Value;
            Assert.Equal(productInfo, identity.ProductInfo);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetBadUsernameInputs))]
        public void NegativeUsernameTest(string username)
        {
            IIdentityFactory factory = new IdentityFactory(Hostname);
            Assert.Throws<EdgeHubConnectionException>(() => factory.GetWithSasToken(username, SasToken).Value);
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
            Try<IIdentity> identity = factory.GetWithSasToken(value, token);
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
