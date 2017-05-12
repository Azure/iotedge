// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Test;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class CloudProxyProviderTest
    {
        readonly ILogger logger;

        public CloudProxyProviderTest()
        {
            ILoggerFactory factory = new LoggerFactory()
                .AddConsole();
            this.logger = factory.CreateLogger<CloudProxyProviderTest>();
        }

        [Fact]
        [Integration]
        public async Task ConnectTest()
        {
            var messageConverter = new Mock<Core.IMessageConverter<Client.Message>>();
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(this.logger, messageConverter.Object);
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
            var messageConverter = new Mock<Core.IMessageConverter<Client.Message>>();
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(this.logger, messageConverter.Object);
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
