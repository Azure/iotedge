// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Test;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class CloudProxyTest
    {
        const int ConnectionPoolSize = 10;
        readonly ILogger logger;

        public CloudProxyTest()
        {
            ILoggerFactory factory = new LoggerFactory()
                .AddConsole();
            this.logger = factory.CreateLogger<CloudProxyTest>();
        }

        public static IEnumerable<object[]> GetTestMessages()
        {
            IList<IMessage> messages = MessageHelper.GenerateMessages(4);
            return new List<object[]> { new object[] { messages } };
        }

        public static IEnumerable<object[]> GetTestMessage()
        {
            IList<IMessage> messages = MessageHelper.GenerateMessages(1);
            return new List<object[]> { new object[] { messages[0] } };
        }

        [Theory]
        [Bvt]
        [MemberData(nameof(GetTestMessage))]
        public async Task SendMessageTest(IMessage message)
        {
            DateTime startTime = DateTime.UtcNow;
            Try<ICloudProxy> cloudProxy = await this.GetCloudProxyWithConnectionStringKey("device1ConnStrKey");
            Assert.True(cloudProxy.Success);
            bool result = await cloudProxy.Value.SendMessage(message);
            Assert.True(result);
            bool disconnectResult = await cloudProxy.Value.CloseAsync();
            Assert.True(disconnectResult);
            await CheckMessageInEventHub(new List<IMessage> { message }, startTime);
        }

        [Theory]
        [Bvt]
        [MemberData(nameof(GetTestMessages))]
        public async Task SendMessageMultipleDevicesTest(IList<IMessage> messages)
        {
            DateTime startTime = DateTime.UtcNow;
            var mockCloudListener = new Mock<ICloudListener>();
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(this.logger, new MessageConverter());

            string device1ConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            Try<ICloudProxy> cloudProxy1 = await cloudProxyProvider.Connect(device1ConnectionString);
            Assert.True(cloudProxy1.Success);

            string device2ConnectionString = await SecretsHelper.GetSecretFromConfigKey("device2ConnStrKey");
            Try<ICloudProxy> cloudProxy2 = await cloudProxyProvider.Connect(device2ConnectionString);
            Assert.True(cloudProxy2.Success);

            for (int i = 0; i < messages.Count; i = i + 2)
            {
                bool result = await cloudProxy1.Value.SendMessage(messages[i]);
                Assert.True(result);
                result = await cloudProxy2.Value.SendMessage(messages[i + 1]);
                Assert.True(result);
            }

            bool disconnectResult = await cloudProxy1.Value.CloseAsync();
            Assert.True(disconnectResult);
            disconnectResult = await cloudProxy2.Value.CloseAsync();
            Assert.True(disconnectResult);

            await CheckMessageInEventHub(messages, startTime);
        }

        [Theory]
        [Bvt]
        [MemberData(nameof(GetTestMessages))]
        public async Task SendMessageBatchTest(IList<IMessage> messages)
        {
            DateTime startTime = DateTime.UtcNow;
            Try<ICloudProxy> cloudProxy = await this.GetCloudProxyWithConnectionStringKey("device1ConnStrKey");
            Assert.True(cloudProxy.Success);
            bool result = await cloudProxy.Value.SendMessageBatch(messages);
            Assert.True(result);
            bool disconnectResult = await cloudProxy.Value.CloseAsync();
            Assert.True(disconnectResult);
            await CheckMessageInEventHub(messages, startTime);
        }

        async Task<Try<ICloudProxy>> GetCloudProxyWithConnectionStringKey(string connectionStringConfigKey)
        {
            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey(connectionStringConfigKey);
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(this.logger, new MessageConverter());
            Try<ICloudProxy> cloudProxy = await cloudProxyProvider.Connect(deviceConnectionString);
            return cloudProxy;
        }

        static async Task CheckMessageInEventHub(IList<IMessage> sentMessages, DateTime startTime)
        {
            string eventHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("eventHubConnStrKey");
            var eventHubReceiver = new EventHubReceiver(eventHubConnectionString);
            IList<EventData> cloudMessages = await eventHubReceiver.GetMessagesFromAllPartitions(startTime);
            await eventHubReceiver.Close();
            Assert.NotNull(cloudMessages);
            Assert.NotEmpty(cloudMessages);
            Assert.True(MessageHelper.CompareMessagesAndEventData(sentMessages, cloudMessages));
        }
    }
}