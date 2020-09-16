// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;
    using IDeviceIdentity = Microsoft.Azure.Devices.ProtocolGateway.Identity.IDeviceIdentity;

    public class IdentityTest
    {
        static readonly string Hostname = "TestHub.azure-devices.net";
        static readonly string DeviceId = "device_2";
        static readonly string ModuleId = "Module_1";
        static readonly string SasToken = TokenHelper.CreateSasToken($"{Hostname}/devices/{DeviceId}");
        static readonly string ApiVersion = "api-version=2016-11-14";
        static readonly string ProductInfo = "don't care";
        static readonly string DeviceClientType = $"DeviceClientType={ProductInfo}";

        public static IEnumerable<object[]> GetIdentityInputs()
        {
            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/{ApiVersion}&{DeviceClientType}",
                DeviceId,
                Hostname,
                SasToken,
                true,
                typeof(TokenCredentials),
                typeof(DeviceIdentity),
                AuthenticationType.Token
            };

            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/{ModuleId}/{ApiVersion}&{DeviceClientType}",
                $"{DeviceId}/{ModuleId}",
                Hostname,
                SasToken,
                true,
                typeof(TokenCredentials),
                typeof(ModuleIdentity),
                AuthenticationType.Token
            };

            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/{ApiVersion}&{DeviceClientType}",
                DeviceId,
                Hostname,
                null,
                true,
                typeof(X509CertCredentials),
                typeof(DeviceIdentity),
                AuthenticationType.X509Cert
            };

            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/{ModuleId}/{ApiVersion}&{DeviceClientType}",
                $"{DeviceId}/{ModuleId}",
                Hostname,
                null,
                true,
                typeof(X509CertCredentials),
                typeof(ModuleIdentity),
                AuthenticationType.X509Cert
            };
        }

        public static IEnumerable<object[]> GetModuleIdentityInputs()
        {
            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/module_1232/{ApiVersion}&{DeviceClientType}",
                Hostname,
                SasToken,
                DeviceId,
                "module_1232",
                AuthenticationType.Token
            };

            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/{ModuleId}/{ApiVersion}&{DeviceClientType}",
                Hostname,
                SasToken,
                DeviceId,
                ModuleId,
                AuthenticationType.Token
            };

            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/module_1232/{ApiVersion}&{DeviceClientType}",
                Hostname,
                null,
                DeviceId,
                "module_1232",
                AuthenticationType.X509Cert
            };

            yield return new object[]
            {
                $"{Hostname}/{DeviceId}/{ModuleId}/{ApiVersion}&{DeviceClientType}",
                Hostname,
                null,
                DeviceId,
                ModuleId,
                AuthenticationType.X509Cert
            };
        }

        public static IEnumerable<object[]> GetIdentityWithProductInfoInputs()
        {
            yield return new[]
            {
                // happy path
                "abc",
                $"{Hostname}/{DeviceId}/{ApiVersion}&{DeviceClientType}",
                $"{ProductInfo} abc",
                ProductInfo
            };

            yield return new[]
            {
                // no DeviceClientType
                "abc",
                $"{Hostname}/{DeviceId}/{ApiVersion}",
                "abc",
                string.Empty
            };

            yield return new[]
            {
                // no caller product info
                string.Empty,
                $"{Hostname}/{DeviceId}/{ApiVersion}&{DeviceClientType}",
                ProductInfo,
                ProductInfo
            };

            yield return new[]
            {
                // no DeviceClientType OR caller product info
                string.Empty,
                $"{Hostname}/{DeviceId}/{ApiVersion}",
                string.Empty,
                string.Empty
            };
        }

        public static IEnumerable<object[]> GetUsernameInputs()
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

        [Theory]
        [Unit]
        [MemberData(nameof(GetIdentityInputs))]
        public async Task GetIdentityTest(
            string value,
            string clientId,
            string iotHubHostName,
            string token,
            bool success,
            Type expectedCredentialsType,
            Type expectedIdentityType,
            AuthenticationType expected)
        {
            X509Certificate2 certificate = new X509Certificate2();
            IList<X509Certificate2> chain = new List<X509Certificate2>();
            IClientCredentials clientCredentials = await GetClientCredentials(iotHubHostName, clientId, value, token, token == null, string.Empty, certificate, chain);
            Assert.NotNull(clientCredentials);
            Assert.IsType(expectedCredentialsType, clientCredentials);
            Assert.IsType(expectedIdentityType, clientCredentials.Identity);
            Assert.Equal(iotHubHostName, ((Identity)clientCredentials.Identity).IotHubHostname);
            Assert.Equal(ProductInfo, clientCredentials.ProductInfo);
            Assert.Equal(expected, clientCredentials.AuthenticationType);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetIdentityWithProductInfoInputs))]
        public async Task GetIdentityWithProductInfoTest(string productInfo, string username, string result, string clientProductInfo)
        {
            string receivedProductInfo = null;
            var metadataStore = new Mock<IMetadataStore>();
            metadataStore.Setup(p => p.SetMetadata(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Option<string>>()))
                .Callback<string, string, Option<string>>((_, p, __) => receivedProductInfo = p)
                .Returns(Task.CompletedTask);

            IClientCredentials clientCredentials = await GetClientCredentials(Hostname, DeviceId, username, SasToken, false, productInfo, metadataStore: metadataStore.Object);
            Assert.NotNull(clientCredentials);
            Assert.Equal(result, clientCredentials.ProductInfo);

            Assert.Equal(clientProductInfo, receivedProductInfo);
            metadataStore.VerifyAll();
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetUsernameInputs))]
        public async Task ProductInfoTest(string username, string clientId, string productInfo)
        {
            string receivedProductInfo = null;
            var metadataStore = new Mock<IMetadataStore>();
            metadataStore.Setup(p => p.SetMetadata(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Option<string>>()))
                .Callback<string, string, Option<string>>((_, p, __) => receivedProductInfo = p)
                .Returns(Task.CompletedTask);

            IClientCredentials clientCredentials = await GetClientCredentials(Hostname, clientId, username, SasToken, metadataStore: metadataStore.Object);
            Assert.NotNull(clientCredentials);
            Assert.Equal(productInfo, clientCredentials.ProductInfo);
            Assert.Equal(productInfo, receivedProductInfo);
            metadataStore.VerifyAll();
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetModuleIdentityInputs))]
        public async Task GetModuleIdentityTest(
            string value,
            string iotHubHostName,
            string token,
            string deviceId,
            string moduleId,
            AuthenticationType authenticationType)
        {
            var certificate = new X509Certificate2();
            var chain = new List<X509Certificate2>();
            IClientCredentials clientCredentials = await GetClientCredentials(iotHubHostName, $"{deviceId}/{moduleId}", value, token, token == null, string.Empty, certificate, chain);
            Assert.NotNull(clientCredentials);
            Assert.Equal(authenticationType, clientCredentials.AuthenticationType);
            var hubModuleIdentity = clientCredentials.Identity as IModuleIdentity;
            Assert.NotNull(hubModuleIdentity);
            Assert.Equal(deviceId, hubModuleIdentity.DeviceId);
            Assert.Equal(moduleId, hubModuleIdentity.ModuleId);
            Assert.Equal($"{deviceId}/{moduleId}", hubModuleIdentity.Id);
        }

        static async Task<IClientCredentials> GetClientCredentials(string iotHubHostName, string deviceId, string userName, string token, bool isCertAuthAllowed = false, string productInfo = "", X509Certificate2 certificate = null, IList<X509Certificate2> chain = null, IMetadataStore metadataStore = null)
        {
            metadataStore = metadataStore ?? Mock.Of<IMetadataStore>();
            var authenticator = Mock.Of<IAuthenticator>(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>()) == Task.FromResult(true));
            var usernameParser = new MqttUsernameParser();
            var factory = new ClientCredentialsFactory(new IdentityProvider(iotHubHostName), productInfo);
            var credentialIdentityProvider = new DeviceIdentityProvider(authenticator, usernameParser, factory, metadataStore, isCertAuthAllowed);
            if (certificate != null && chain != null)
            {
                credentialIdentityProvider.RegisterConnectionCertificate(certificate, chain);
            }

            IDeviceIdentity deviceIdentity = await credentialIdentityProvider.GetAsync(deviceId, userName, token, null);
            Assert.NotNull(deviceIdentity);
            IClientCredentials clientCredentials = (deviceIdentity as ProtocolGatewayIdentity)?.ClientCredentials;
            return clientCredentials;
        }
    }
}
