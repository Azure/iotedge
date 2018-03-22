// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Moq;
    using Xunit;

    public class DeviceIdentityProviderTest
    {
        static IEnumerable<object[]> GetIdentityProviderInputs()
        {
            string sasToken = TokenHelper.CreateSasToken("TestHub.azure-devices.net/devices/device_2", "AAAAAAAAAAAzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz");
            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "Device_2",
                $"127.0.0.1/Device_2/api-version=2016-11-14&DeviceClientType={Uri.EscapeDataString("Microsoft.Azure.Devices.Client/1.2.2")}",
                sasToken,
                true,
                typeof(ProtocolGatewayIdentity)
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "Device_1",
                $"127.0.0.1/Device_2/api-version=2016-11-14&DeviceClientType={Uri.EscapeDataString("Microsoft.Azure.Devices.Client/1.2.2")}",
                sasToken,
                true,
                typeof(UnauthenticatedDeviceIdentity)
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "Device_2",
                $"127.0.0.1/Device_2/api-version=2016-11-14&DeviceClientType={Uri.EscapeDataString("Microsoft.Azure.Devices.Client/1.2.2")}",
                sasToken,
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
            bool authRetVal,
            Type expectedType)
        {
            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(authRetVal);

            IDeviceIdentityProvider deviceIdentityProvider = new DeviceIdentityProvider(authenticator.Object, new ClientCredentialsFactory(iotHubHostName), false);
            IDeviceIdentity deviceIdentity = await deviceIdentityProvider.GetAsync(clientId, username, password, null);
            Assert.IsAssignableFrom(expectedType, deviceIdentity);
        }
    }
}
