// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class CloudConnectionProviderTest
    {
        static readonly Core.IMessageConverterProvider MessageConverterProvider = Mock.Of<IMessageConverterProvider>();
        const int ConnectionPoolSize = 10;

        [Fact]
        [Integration]
        public async Task ConnectTest()
        {
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(MessageConverterProvider, ConnectionPoolSize, new DeviceClientProvider());
            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            var deviceIdentity = Mock.Of<IIdentity>(m => m.Id == ConnectionStringHelper.GetDeviceId(deviceConnectionString) && m.ConnectionString == deviceConnectionString);
            Try<ICloudConnection> cloudProxy = cloudConnectionProvider.Connect(deviceIdentity, null).Result;
            Assert.True(cloudProxy.Success);
            bool result = await cloudProxy.Value.CloseAsync();
            Assert.True(result);
        }

        [Fact]
        [Integration]
        public async Task ConnectWithInvalidConnectionStringTest()
        {
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(MessageConverterProvider, ConnectionPoolSize, new DeviceClientProvider());
            var deviceIdentity1 = Mock.Of<IIdentity>(m => m.Id == "device1" && m.ConnectionString == string.Empty && m.Token == Option.None<string>());
            Try<ICloudConnection> result = await cloudConnectionProvider.Connect(deviceIdentity1, null);
            Assert.False(result.Success);
            Assert.IsType<ArgumentException>(result.Exception);

            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            // Change the connection string key, deliberately.
            char updatedLastChar = (char)(deviceConnectionString[deviceConnectionString.Length - 1] + 1);
            deviceConnectionString = deviceConnectionString.Substring(0, deviceConnectionString.Length - 1) + updatedLastChar;
            var deviceIdentity2 = Mock.Of<IIdentity>(m => m.Id == ConnectionStringHelper.GetDeviceId(deviceConnectionString) && m.ConnectionString == deviceConnectionString);
            Try<ICloudConnection> cloudProxy = cloudConnectionProvider.Connect(deviceIdentity2, null).Result;
            Assert.False(cloudProxy.Success);
        }        
    }
}
