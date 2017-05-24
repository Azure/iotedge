// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    public class CloudProxyProviderTest
    {
        static readonly Core.IMessageConverter<Client.Message> MessageConverter = Mock.Of<Core.IMessageConverter<Client.Message>>();
        static readonly Core.IMessageConverter<Twin> TwinConverter = Mock.Of<Core.IMessageConverter<Twin>>();

        [Fact]
        [Integration]
        public async Task ConnectTest()
        {
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(MessageConverter, TwinConverter);
            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            var deviceIdentity = Mock.Of<IIdentity>(m => m.Id == "device1" && m.ConnectionString == deviceConnectionString);
            Try<ICloudProxy> cloudProxy = cloudProxyProvider.Connect(deviceIdentity).Result;
            Assert.True(cloudProxy.Success);
            bool result = await cloudProxy.Value.CloseAsync();
            Assert.True(result);
        }

        [Fact]
        [Integration]
        public async Task ConnectWithInvalidConnectionStringTest()
        {
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(MessageConverter, TwinConverter);
            var deviceIdentity1 = Mock.Of<IIdentity>(m => m.Id == "device1" && m.ConnectionString == string.Empty);
            await Assert.ThrowsAsync<ArgumentException>(() => cloudProxyProvider.Connect(deviceIdentity1));

            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            // Change the connection string key, deliberately.
            char updatedLastChar = (char)(deviceConnectionString[deviceConnectionString.Length - 1] + 1);
            deviceConnectionString = deviceConnectionString.Substring(0, deviceConnectionString.Length - 1) + updatedLastChar;
            var deviceIdentity2 = Mock.Of<IIdentity>(m => m.Id == "device1" && m.ConnectionString == deviceConnectionString);
            Try<ICloudProxy> cloudProxy = cloudProxyProvider.Connect(deviceIdentity2).Result;
            Assert.False(cloudProxy.Success);
        }
    }
}
