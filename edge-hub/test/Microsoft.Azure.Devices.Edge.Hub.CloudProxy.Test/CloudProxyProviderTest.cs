// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class CloudProxyProviderTest
    {
        static readonly ILoggerFactory LoggerFactory = new LoggerFactory().AddConsole();
        static readonly Core.IMessageConverter<Client.Message> MessageConverter = Mock.Of<Core.IMessageConverter<Client.Message>>();
        static readonly Core.IMessageConverter<Twin> TwinConverter = Mock.Of<Core.IMessageConverter<Twin>>();

        [Fact]
        [Integration]
        public async Task ConnectTest()
        {
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(MessageConverter, TwinConverter, LoggerFactory);
            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            Try<ICloudProxy> cloudProxy = cloudProxyProvider.Connect(deviceConnectionString).Result;
            Assert.True(cloudProxy.Success);
            bool result = await cloudProxy.Value.CloseAsync();
            Assert.True(result);
        }

        [Fact]
        [Integration]
        public async Task ConnectWithInvalidConnectionStringTest()
        {
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(MessageConverter, TwinConverter, LoggerFactory);
            await Assert.ThrowsAsync<ArgumentException>(() => cloudProxyProvider.Connect(""));

            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            // Change the connection string key, deliberately.
            char updatedLastChar = (char)(deviceConnectionString[deviceConnectionString.Length - 1] + 1);
            deviceConnectionString = deviceConnectionString.Substring(0, deviceConnectionString.Length - 1) + updatedLastChar;
            Try<ICloudProxy> cloudProxy = cloudProxyProvider.Connect(deviceConnectionString).Result;
            Assert.False(cloudProxy.Success);
        }
    }
}
