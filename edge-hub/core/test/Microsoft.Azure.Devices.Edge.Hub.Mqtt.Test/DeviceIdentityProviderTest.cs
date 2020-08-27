// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.Routing.Core.Query.Builtins;
    using Moq;
    using Xunit;

    public class DeviceIdentityProviderTest
    {
        public static IEnumerable<object[]> GetIdentityProviderInputs()
        {
            string sasToken = TokenHelper.CreateSasToken("TestHub.azure-devices.net/devices/device_2", "AAAAAAAAAAAzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz");
            var certificate = new X509Certificate2();
            IList<X509Certificate2> chain = new List<X509Certificate2> { certificate };

            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "Device_2",
                $"127.0.0.1/Device_2/api-version=2016-11-14&DeviceClientType={Uri.EscapeDataString("Microsoft.Azure.Devices.Client/1.2.2")}",
                sasToken,
                null,
                null,
                true,
                typeof(ProtocolGatewayIdentity)
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "Device_1",
                $"127.0.0.1/Device_2/api-version=2016-11-14&DeviceClientType={Uri.EscapeDataString("Microsoft.Azure.Devices.Client/1.2.2")}",
                sasToken,
                null,
                null,
                true,
                typeof(UnauthenticatedDeviceIdentity)
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "Device_2",
                $"127.0.0.1/Device_2/api-version=2016-11-14&DeviceClientType={Uri.EscapeDataString("Microsoft.Azure.Devices.Client/1.2.2")}",
                sasToken,
                null,
                null,
                false,
                typeof(UnauthenticatedDeviceIdentity)
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "Device_2",
                $"127.0.0.1/Device_2/api-version=2016-11-14&DeviceClientType={Uri.EscapeDataString("Microsoft.Azure.Devices.Client/1.2.2")}",
                null,
                certificate,
                chain,
                true,
                typeof(ProtocolGatewayIdentity)
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "Device_1",
                $"127.0.0.1/Device_2/api-version=2016-11-14&DeviceClientType={Uri.EscapeDataString("Microsoft.Azure.Devices.Client/1.2.2")}",
                null,
                certificate,
                chain,
                true,
                typeof(UnauthenticatedDeviceIdentity)
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "Device_2",
                $"127.0.0.1/Device_2/api-version=2016-11-14&DeviceClientType={Uri.EscapeDataString("Microsoft.Azure.Devices.Client/1.2.2")}",
                null,
                certificate,
                chain,
                false,
                typeof(UnauthenticatedDeviceIdentity)
            };
        }

        [Theory]
        [Integration]
        [MemberData(nameof(GetIdentityProviderInputs))]
        public async Task GetDeviceIdentityTest(
            string iotHubHostName,
            string clientId,
            string username,
            string password,
            X509Certificate2 certificate,
            IList<X509Certificate2> chain,
            bool authRetVal,
            Type expectedType)
        {
            var authenticator = new Mock<IAuthenticator>();
            var metadataStore = Mock.Of<IMetadataStore>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(authRetVal);
            var deviceIdentityProvider = new DeviceIdentityProvider(authenticator.Object, new MqttUsernameParser(), new ClientCredentialsFactory(new IdentityProvider(iotHubHostName)), metadataStore, true);
            if (certificate != null)
            {
                deviceIdentityProvider.RegisterConnectionCertificate(certificate, chain);
            }

            IDeviceIdentity deviceIdentity = await deviceIdentityProvider.GetAsync(clientId, username, password, null);
            Assert.IsAssignableFrom(expectedType, deviceIdentity);
        }

        [Fact]
        [Unit]
        public async Task GetIdentityCertAuthNotEnabled()
        {
            string iotHubHostName = "foo.azure-devices.net";
            var authenticator = new Mock<IAuthenticator>();
            var metadataStore = Mock.Of<IMetadataStore>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);
            var deviceIdentityProvider = new DeviceIdentityProvider(authenticator.Object, new MqttUsernameParser(), new ClientCredentialsFactory(new IdentityProvider(iotHubHostName)), metadataStore, false);
            deviceIdentityProvider.RegisterConnectionCertificate(new X509Certificate2(), new List<X509Certificate2> { new X509Certificate2() });
            IDeviceIdentity deviceIdentity = await deviceIdentityProvider.GetAsync(
                "Device_2",
                $"127.0.0.1/Device_2/api-version=2016-11-14&DeviceClientType={Uri.EscapeDataString("Microsoft.Azure.Devices.Client/1.2.2")}",
                null,
                null);
            Assert.Equal(UnauthenticatedDeviceIdentity.Instance, deviceIdentity);
        }
    }
}
