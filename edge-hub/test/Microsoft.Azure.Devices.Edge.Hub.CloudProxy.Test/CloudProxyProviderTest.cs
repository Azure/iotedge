// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class CloudProxyProviderTest
    {
        static readonly Core.IMessageConverterProvider MessageConverterProvider = Mock.Of<IMessageConverterProvider>();
        const int ConnectionPoolSize = 10;

        [Fact]
        [Integration]
        public async Task ConnectTest()
        {
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(MessageConverterProvider, ConnectionPoolSize, true);
            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            var deviceIdentity = Mock.Of<IIdentity>(m => m.Id == ConnectionStringHelper.GetDeviceId(deviceConnectionString) && m.ConnectionString == deviceConnectionString);
            Try<ICloudProxy> cloudProxy = cloudProxyProvider.Connect(deviceIdentity, null).Result;
            Assert.True(cloudProxy.Success);
            bool result = await cloudProxy.Value.CloseAsync();
            Assert.True(result);
        }

        [Fact]
        [Integration]
        public async Task ConnectWithInvalidConnectionStringTest()
        {
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(MessageConverterProvider, ConnectionPoolSize, true);
            var deviceIdentity1 = Mock.Of<IIdentity>(m => m.Id == "device1" && m.ConnectionString == string.Empty);
            await Assert.ThrowsAsync<ArgumentException>(() => cloudProxyProvider.Connect(deviceIdentity1, null));

            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            // Change the connection string key, deliberately.
            char updatedLastChar = (char)(deviceConnectionString[deviceConnectionString.Length - 1] + 1);
            deviceConnectionString = deviceConnectionString.Substring(0, deviceConnectionString.Length - 1) + updatedLastChar;
            var deviceIdentity2 = Mock.Of<IIdentity>(m => m.Id == ConnectionStringHelper.GetDeviceId(deviceConnectionString) && m.ConnectionString == deviceConnectionString);
            Try<ICloudProxy> cloudProxy = cloudProxyProvider.Connect(deviceIdentity2, null).Result;
            Assert.False(cloudProxy.Success);
        }

        [Fact]
        [Unit]
        public void TestGetOperationTimeoutMilliseconds()
        {
            var tokenExpiryTime = new DateTime(2020, 1, 1);
            string deviceId = "device_2";
            string iotHubHostName = "TestHub.azure-devices.net";
            string sasToken = TokenHelper.CreateSasToken($"{iotHubHostName}/devices/{deviceId}", tokenExpiryTime);
            IAuthenticationMethod authenticationMethod = new DeviceAuthenticationWithToken(deviceId, sasToken);
            IotHubConnectionStringBuilder csb = IotHubConnectionStringBuilder.Create(iotHubHostName, authenticationMethod);
            string connectionString = csb.ToString();
            
            uint timeout = CloudProxyProvider.GetOperationTimeoutMilliseconds(connectionString);

            uint timeoutLowerLimit = (uint)(tokenExpiryTime - DateTime.UtcNow).TotalMilliseconds;
            Assert.InRange<uint>(timeout, timeoutLowerLimit, timeoutLowerLimit + 100);
        }
    }
}
