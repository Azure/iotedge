// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

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
                DeviceId,
                Hostname,
                SasToken,
                true,
                typeof(DeviceIdentity)
            };

            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/{ModuleId}/{ApiVersion}&{DeviceClientType}",
                $"{DeviceId}/{ModuleId}",
                Hostname,
                SasToken,
                true,
                typeof(ModuleIdentity)
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
            yield return new[]
            {   // happy path
                "abc",
                $"{Hostname}/{DeviceId}/{ApiVersion}&{DeviceClientType}",
                $"abc {ProductInfo}"
            };

            yield return new[]
            {   // no DeviceClientType
                "abc",
                $"{Hostname}/{DeviceId}/{ApiVersion}",
                "abc"
            };

            yield return new[]
            {   // no caller product info
                string.Empty,
                $"{Hostname}/{DeviceId}/{ApiVersion}&{DeviceClientType}",
                ProductInfo
            };

            yield return new[]
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
            string clientId = $"{DeviceId}/{ModuleId}";

            yield return new[]
            {
                $"{devicePrefix}",
                DeviceId,
                string.Empty
            };

            yield return new[]
            {
                $"{modulePrefix}",
                clientId,
                string.Empty
            };

            yield return new[]
            {
                $"{devicePrefix}&DeviceClientType=",
                DeviceId,
                string.Empty
            };

            yield return new[]
            {
                $"{modulePrefix}&DeviceClientType=",
                clientId,
                string.Empty
            };

            yield return new[]
            {
                $"{devicePrefix}&{DeviceClientType}",
                DeviceId,
                ProductInfo
            };

            yield return new[]
            {
                $"{modulePrefix}&{DeviceClientType}",
                clientId,
                ProductInfo
            };

            yield return new[]
            {
                $"{Hostname}/{DeviceId}/{DeviceClientType}&{ApiVersion}",
                DeviceId,
                ProductInfo
            };

            yield return new[]
            {
                $"{Hostname}/{DeviceId}/{ModuleId}/{DeviceClientType}&{ApiVersion}",
                clientId,
                ProductInfo
            };

            yield return new[]
            {
                $"{devicePrefix}&{DeviceClientType}&DeviceClientType=abc123",
                DeviceId,
                ProductInfo
            };

            yield return new[]
            {
                $"{devicePrefix}&{DeviceClientType}=abc123",
                DeviceId,
                $"{ProductInfo}=abc123"
            };
        }

        static IEnumerable<string[]> GetBadUsernameInputs()
        {
            yield return new[] { "missingEverythingAfterHostname" };
            yield return new[] { "hostname/missingEverthingAfterDeviceId" };
            yield return new[] { "hostname/deviceId/missingApiVersionProperty" };
            yield return new[] { "hostname/deviceId/moduleId/missingApiVersionProperty" };
            yield return new[] { "hostname/deviceId/moduleId/stillMissingApiVersionProperty&DeviceClientType=whatever" };
            yield return new[] { "hostname/deviceId/moduleId/DeviceClientType=whatever&stillMissingApiVersionProperty" };
            yield return new[] { "hostname/deviceId/moduleId/DeviceClientType=stillMissingApiVersionProperty" };
            yield return new[] { "hostname/deviceId/moduleId/api-version=whatever/tooManySegments" };
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetIdentityInputs))]
        public async Task GetIdentityTest(string value,
            string clientId,
            string iotHubHostName,
            string token,
            bool success,
            Type expectedType)
        {
            IIdentity identity = await GetIdentity(iotHubHostName, clientId, value, token);
            Assert.NotNull(identity);

            Assert.NotNull(identity);
            Assert.IsType(expectedType, identity);
            Assert.Equal(iotHubHostName, ((Identity)identity).IotHubHostName);
            Assert.Equal(ProductInfo, identity.ProductInfo);
        }


        [Theory]
        [Unit]
        [MemberData(nameof(GetIdentityWithProductInfoInputs))]
        public async Task GetIdentityWithProductInfoTest(string productInfo, string username, string result)
        {
            IIdentity identity = await GetIdentity(Hostname, DeviceId, username, SasToken, productInfo);
            Assert.NotNull(identity);
            Assert.Equal(result, identity.ProductInfo);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetUsernameInputs))]
        public async Task ProductInfoTest(string username, string clientId, string productInfo)
        {
            IIdentity identity = await GetIdentity(Hostname, clientId, username, SasToken);
            Assert.NotNull(identity);
            Assert.Equal(productInfo, identity.ProductInfo);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetBadUsernameInputs))]
        public void NegativeUsernameTest(string username)
        {
            Assert.Throws<EdgeHubConnectionException>(() => SasTokenDeviceIdentityProvider.ParseUserName(username));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetModuleIdentityInputs))]
        public async Task GetModuleIdentityTest(string value,
            string iotHubHostName,
            string token,
            string deviceId,
            string moduleId)
        {
            IIdentity identity = await GetIdentity(iotHubHostName, $"{deviceId}/{moduleId}", value, token);
            Assert.NotNull(identity);
            var hubModuleIdentity = identity as IModuleIdentity;
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

        static async Task<IIdentity> GetIdentity(string iotHubHostName, string deviceId, string userName, string token, string productInfo = "")
        {
            var authenticator = Mock.Of<IAuthenticator>(a => a.AuthenticateAsync(It.IsAny<IIdentity>()) == Task.FromResult(true));
            var factory = new IdentityFactory(iotHubHostName, productInfo);
            var sasTokenIdentityProvider = new SasTokenDeviceIdentityProvider(authenticator, factory);

            ProtocolGateway.Identity.IDeviceIdentity deviceIdentity = await sasTokenIdentityProvider.GetAsync(deviceId, userName, token, null);
            Assert.NotNull(deviceIdentity);
            IIdentity identity = (deviceIdentity as ProtocolGatewayIdentity)?.Identity;
            return identity;
        }
    }
}
