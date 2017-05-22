// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;
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

            Try<ICloudProxy> cloudProxy1 = await this.GetCloudProxyWithConnectionStringKey("device1ConnStrKey");
            Assert.True(cloudProxy1.Success);

            Try<ICloudProxy> cloudProxy2 = await this.GetCloudProxyWithConnectionStringKey("device2ConnStrKey");
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

        [Fact]
        [Bvt]
        public async Task CanGetTwin()
        {
            Try<ICloudProxy> cloudProxy = await this.GetCloudProxyWithConnectionStringKey("device1ConnStrKey");
            Assert.True(cloudProxy.Success);
            IMessage result = await cloudProxy.Value.GetTwinAsync();
            string actualString = System.Text.Encoding.UTF8.GetString(result.Body);
            Assert.StartsWith("{", actualString);
            bool disconnectResult = await cloudProxy.Value.CloseAsync();
            Assert.True(disconnectResult);
        }

        [Fact]
        [Bvt]
        public async Task CanUpdateReportedProperties()
        {
            Try<ICloudProxy> cloudProxy = await this.GetCloudProxyWithConnectionStringKey("device1ConnStrKey");
            Assert.True(cloudProxy.Success);
            IMessage message = await cloudProxy.Value.GetTwinAsync();

            JObject twin = JObject.Parse(System.Text.Encoding.UTF8.GetString(message.Body));
            int version = (int)twin.SelectToken("reported.$version");
            int counter = (int?)twin.SelectToken("reported.bvtCounter") ?? 0;

            string updated = $"{{\"bvtCounter\":{counter + 1}}}";
            await cloudProxy.Value.UpdateReportedPropertiesAsync(updated);

            message = await cloudProxy.Value.GetTwinAsync();
            twin = JObject.Parse(System.Text.Encoding.UTF8.GetString(message.Body));
            int nextVersion = (int)twin.SelectToken("reported.$version");
            var nextCounter = (int?)twin.SelectToken("reported.bvtCounter");
            Assert.NotNull(nextCounter);
            Assert.Equal(version + 1, nextVersion);
            Assert.Equal(counter + 1, nextCounter);

            bool disconnectResult = await cloudProxy.Value.CloseAsync();
            Assert.True(disconnectResult);
        }

        async Task<Try<ICloudProxy>> GetCloudProxyWithConnectionStringKey(string connectionStringConfigKey)
        {
            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey(connectionStringConfigKey);
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(new MqttMessageConverter(), new TwinMessageConverter(), LoggerFactory);
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