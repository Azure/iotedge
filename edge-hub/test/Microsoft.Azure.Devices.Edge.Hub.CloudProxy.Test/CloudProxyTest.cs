// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class CloudProxyTest
    {
        static readonly ILoggerFactory LoggerFactory = new LoggerFactory().AddConsole();

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
            bool result = await cloudProxy.Value.SendMessageAsync(message);
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
            var messageConverter = new Mock<IMessageConverter<Client.Message>>();
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(messageConverter.Object, LoggerFactory);

            string device1ConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            Try<ICloudProxy> cloudProxy1 = await cloudProxyProvider.Connect(device1ConnectionString);
            Assert.True(cloudProxy1.Success);

            string device2ConnectionString = await SecretsHelper.GetSecretFromConfigKey("device2ConnStrKey");
            Try<ICloudProxy> cloudProxy2 = await cloudProxyProvider.Connect(device2ConnectionString);
            Assert.True(cloudProxy2.Success);

            for (int i = 0; i < messages.Count; i = i + 2)
            {
                bool result = await cloudProxy1.Value.SendMessageAsync(messages[i]);
                Assert.True(result);
                result = await cloudProxy2.Value.SendMessageAsync(messages[i + 1]);
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
            bool result = await cloudProxy.Value.SendMessageBatchAsync(messages);
            Assert.True(result);
            bool disconnectResult = await cloudProxy.Value.CloseAsync();
            Assert.True(disconnectResult);
            await CheckMessageInEventHub(messages, startTime);
        }

        async Task<Try<ICloudProxy>> GetCloudProxyWithConnectionStringKey(string connectionStringConfigKey)
        {
            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey(connectionStringConfigKey);
            var messageConverter = new Mock<Core.IMessageConverter<Client.Message>>();
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(messageConverter.Object, LoggerFactory);
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