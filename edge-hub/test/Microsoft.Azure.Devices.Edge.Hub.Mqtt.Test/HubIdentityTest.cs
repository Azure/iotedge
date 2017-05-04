// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using System.Security.Authentication;
    using System.Security.Cryptography;
    using System.Text;
    using System.Web;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class HubIdentityTest
    {
        static IEnumerable<object[]> GetConnectionStringInputs()
        {
            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "device_2",
                AuthenticationScope.SasToken,
                null,
                TokenHelper.CreateSasToken("TestHub.azure-devices.net/devices/device_2", "AAAAAAAAAAAzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz"),
                "HostName=TestHub.azure-devices.net;DeviceId=device_2;SharedAccessSignature=SharedAccessSignature sr=TestHub.azure-devices.net%2fdevices%2fdevice_2&sig=0Uo41jpwZ83yFiRMEHqrI9f8TWYAHmOEhGFI3nrn54Q%3d&se=1577836800;X509Cert=False"
            };
        }

        static IEnumerable<object[]> GetHubDeviceIdentityInputs()
        {
            string sasToken = TokenHelper.CreateSasToken("TestHub.azure-devices.net/devices/device_2", "AAAAAAAAAAAzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz");
            yield return new object[]
            {
                "TestHub.azure-devices.net/Device_2/api-version=2016-11-14/DeviceClientType=Microsoft.Azure.Devices.Client/1.2.2",
                "TestHub.azure-devices.net",
                sasToken,
                null
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net",
                "TestHub.azure-devices.net",
                sasToken,
                typeof(InvalidCredentialException)
            };

            yield return new object[]
            {
                "TestHub.azure-devices.net/Device_2/api-version=2016-11-14/DeviceClientType=Microsoft.Azure.Devices.Client/1.2.2",
                "TestHub.azure-devices.net",
                sasToken.Substring(0, sasToken.Length - 20),
                typeof(FormatException)
            };
        }        

        [Theory]
        [Unit]
        [MemberData(nameof(GetHubDeviceIdentityInputs))]
        public void GetHubDeviceIdentityTest(string value,
            string iotHubHostName,
            string token,
            Type expectedExceptionType)
        {
            Try<HubDeviceIdentity> identity = HubIdentityHelper.TryGetHubDeviceIdentityWithSasToken(value, iotHubHostName, token);
            Assert.NotNull(identity);
            Assert.Equal(expectedExceptionType == null, identity.Success);
            if (identity.Success)
            {
                Assert.NotNull(identity.Value);
                Assert.Equal(iotHubHostName, identity.Value.IotHubHostName);
            }
            else
            {
                Assert.Equal(expectedExceptionType, identity.Exception.GetType());
            }
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
            string connectionString = HubIdentityHelper.GetConnectionString(iotHubHostName, deviceId, AuthenticationScope.SasToken, policyName, secret);
            Assert.Equal(expectedConnectionString, connectionString);
        }
    }
}