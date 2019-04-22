// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.EventHubs;
    using Moq;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test")]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class CloudProxyTest
    {
        static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

        static readonly int EventHubMessageReceivedRetry = 5;

        [Fact]
        [TestPriority(401)]
        public async Task SendMessageTest()
        {
            IMessage message = MessageHelper.GenerateMessages(1)[0];
            DateTime startTime = DateTime.UtcNow.Subtract(ClockSkew);
            ICloudProxy cloudProxy = await this.GetCloudProxyWithConnectionStringKey("device2ConnStrKey");
            await cloudProxy.SendMessageAsync(message);
            bool disconnectResult = await cloudProxy.CloseAsync();
            Assert.True(disconnectResult);

            await CheckMessageInEventHub(new List<IMessage> { message }, startTime);
        }

        [Fact]
        [TestPriority(402)]
        public async Task SendMessageMultipleDevicesTest()
        {
            IList<IMessage> messages = MessageHelper.GenerateMessages(4);
            DateTime startTime = DateTime.UtcNow.Subtract(ClockSkew);

            ICloudProxy cloudProxy1 = await this.GetCloudProxyWithConnectionStringKey("device2ConnStrKey");

            ICloudProxy cloudProxy2 = await this.GetCloudProxyWithConnectionStringKey("device3ConnStrKey");

            for (int i = 0; i < messages.Count; i = i + 2)
            {
                await cloudProxy1.SendMessageAsync(messages[i]);
                await cloudProxy2.SendMessageAsync(messages[i + 1]);
            }

            bool disconnectResult = await cloudProxy1.CloseAsync();
            Assert.True(disconnectResult);
            disconnectResult = await cloudProxy2.CloseAsync();
            Assert.True(disconnectResult);

            await CheckMessageInEventHub(messages, startTime);
        }

        [Fact]
        [TestPriority(403)]
        public async Task SendMessageBatchTest()
        {
            IList<IMessage> messages = MessageHelper.GenerateMessages(4);
            DateTime startTime = DateTime.UtcNow.Subtract(ClockSkew);
            ICloudProxy cloudProxy = await this.GetCloudProxyWithConnectionStringKey("device2ConnStrKey");
            await cloudProxy.SendMessageBatchAsync(messages);
            bool disconnectResult = await cloudProxy.CloseAsync();
            Assert.True(disconnectResult);

            await CheckMessageInEventHub(messages, startTime);
        }

        [Fact]
        [TestPriority(404)]
        public async Task CanGetTwin()
        {
            ICloudProxy cloudProxy = await this.GetCloudProxyWithConnectionStringKey("device2ConnStrKey");
            IMessage result = await cloudProxy.GetTwinAsync();
            string actualString = Encoding.UTF8.GetString(result.Body);
            Assert.StartsWith("{", actualString);
            bool disconnectResult = await cloudProxy.CloseAsync();
            Assert.True(disconnectResult);
        }

        [Fact]
        [TestPriority(405)]
        public async Task CanUpdateReportedProperties()
        {
            ICloudProxy cloudProxy = await this.GetCloudProxyWithConnectionStringKey("device2ConnStrKey");
            IMessage message = await cloudProxy.GetTwinAsync();

            JObject twin = JObject.Parse(Encoding.UTF8.GetString(message.Body));
            int version = (int)twin.SelectToken("reported.$version");
            int counter = (int?)twin.SelectToken("reported.bvtCounter") ?? 0;

            IMessage updateReportedPropertiesMessage = new EdgeMessage.Builder(Encoding.UTF8.GetBytes($"{{\"bvtCounter\":{counter + 1}}}")).Build();
            await cloudProxy.UpdateReportedPropertiesAsync(updateReportedPropertiesMessage);

            message = await cloudProxy.GetTwinAsync();
            twin = JObject.Parse(Encoding.UTF8.GetString(message.Body));
            int nextVersion = (int)twin.SelectToken("reported.$version");
            var nextCounter = (int?)twin.SelectToken("reported.bvtCounter");
            Assert.NotNull(nextCounter);
            Assert.Equal(version + 1, nextVersion);
            Assert.Equal(counter + 1, nextCounter);

            bool disconnectResult = await cloudProxy.CloseAsync();
            Assert.True(disconnectResult);
        }

        [Fact]
        [TestPriority(406)]
        public async Task CanListenForDesiredPropertyUpdates()
        {
            var update = new TaskCompletionSource<IMessage>();
            var edgeHub = new Mock<IEdgeHub>();
            string deviceConnectionStringKey = "device2ConnStrKey";
            edgeHub.Setup(x => x.UpdateDesiredPropertiesAsync(It.IsAny<string>(), It.IsAny<IMessage>()))
                .Callback((string _, IMessage m) => update.TrySetResult(m))
                .Returns(TaskEx.Done);

            ICloudProxy cloudProxy = await this.GetCloudProxyWithConnectionStringKey(deviceConnectionStringKey, edgeHub.Object);

            await cloudProxy.SetupDesiredPropertyUpdatesAsync();

            var desired = new TwinCollection()
            {
                ["desiredPropertyTest"] = Guid.NewGuid().ToString()
            };

            await UpdateDesiredProperty(ConnectionStringHelper.GetDeviceId(await SecretsHelper.GetSecretFromConfigKey(deviceConnectionStringKey)), desired);
            await update.Task;
            await cloudProxy.RemoveDesiredPropertyUpdatesAsync();

            IMessage expected = new EdgeMessage.Builder(Encoding.UTF8.GetBytes(desired.ToJson())).Build();
            expected.SystemProperties[SystemProperties.EnqueuedTime] = string.Empty;
            expected.SystemProperties[SystemProperties.Version] = desired.Version.ToString();
            IMessage actual = update.Task.Result;

            Assert.Equal(expected.Body, actual.Body);
            Assert.Equal(expected.Properties, actual.Properties);
            Assert.Equal(expected.SystemProperties.Keys, actual.SystemProperties.Keys);
        }

        [Fact]
        [TestPriority(407)]
        public async Task CloudProxyNullReceiverTest()
        {
            // Arrange
            string deviceConnectionStringKey = "device2ConnStrKey";
            ICloudProxy cloudProxy = await this.GetCloudProxyWithConnectionStringKey(deviceConnectionStringKey);

            // Act/assert
            // Without setting up the cloudlistener, the following methods should not throw.
            await cloudProxy.SetupCallMethodAsync();
            await cloudProxy.RemoveCallMethodAsync();
            await cloudProxy.SetupDesiredPropertyUpdatesAsync();
            await cloudProxy.RemoveDesiredPropertyUpdatesAsync();
            cloudProxy.StartListening();
        }

        [Fact]
        public async Task TestCloseThrows()
        {
            // Arrange
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            string clientId = "d1";
            var cloudListener = Mock.Of<ICloudListener>();
            TimeSpan idleTimeout = TimeSpan.FromSeconds(60);
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler = (s, status) => { };
            var client = new Mock<IClient>();
            client.Setup(c => c.CloseAsync()).ThrowsAsync(new InvalidOperationException());
            var cloudProxy = new CloudProxy(client.Object, messageConverterProvider, clientId, connectionStatusChangedHandler, cloudListener, idleTimeout, false);

            // Act
            bool result = await cloudProxy.CloseAsync();

            // Assert.
            Assert.True(result);
            client.VerifyAll();
        }

        [Fact]
        public async Task TestHandlNre()
        {
            // Arrange
            var messageConverter = Mock.Of<IMessageConverter<Message>>(m => m.FromMessage(It.IsAny<IMessage>()) == new Message());
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>(m => m.Get<Message>() == messageConverter);
            string clientId = "d1";
            var cloudListener = Mock.Of<ICloudListener>();
            TimeSpan idleTimeout = TimeSpan.FromSeconds(60);
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler = (s, status) => { };
            var client = new Mock<IClient>(MockBehavior.Strict);
            client.Setup(c => c.SendEventAsync(It.IsAny<Message>())).ThrowsAsync(new NullReferenceException());
            client.Setup(c => c.CloseAsync()).Returns(Task.CompletedTask);
            var cloudProxy = new CloudProxy(client.Object, messageConverterProvider, clientId, connectionStatusChangedHandler, cloudListener, idleTimeout, false);
            IMessage message = new EdgeMessage.Builder(new byte[0]).Build();

            // Act
            await Assert.ThrowsAsync<NullReferenceException>(() => cloudProxy.SendMessageAsync(message));

            // Assert.
            client.VerifyAll();
        }

        static async Task CheckMessageInEventHub(IList<IMessage> sentMessages, DateTime startTime)
        {
            string eventHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("eventHubConnStrKey");
            var eventHubReceiver = new EventHubReceiver(eventHubConnectionString);
            var cloudMessages = new List<EventData>();
            bool messagesFound = false;
            // Add retry mechanism to make sure all the messages sent reached Event Hub. Retry 3 times.
            for (int i = 0; i < EventHubMessageReceivedRetry; i++)
            {
                cloudMessages.AddRange(await eventHubReceiver.GetMessagesFromAllPartitions(startTime));
                messagesFound = MessageHelper.CompareMessagesAndEventData(sentMessages, cloudMessages);
                if (messagesFound)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(20));
            }

            await eventHubReceiver.Close();
            Assert.NotNull(cloudMessages);
            Assert.NotEmpty(cloudMessages);
            Assert.True(messagesFound);
        }

        static async Task UpdateDesiredProperty(string deviceId, TwinCollection desired)
        {
            string connectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            Twin twin = await registryManager.GetTwinAsync(deviceId);
            twin.Properties.Desired = desired;
            twin = await registryManager.UpdateTwinAsync(deviceId, twin, twin.ETag);
            desired["$version"] = twin.Properties.Desired.Version;
        }

        Task<ICloudProxy> GetCloudProxyWithConnectionStringKey(string connectionStringConfigKey) =>
            this.GetCloudProxyWithConnectionStringKey(connectionStringConfigKey, Mock.Of<IEdgeHub>());

        async Task<ICloudProxy> GetCloudProxyWithConnectionStringKey(string connectionStringConfigKey, IEdgeHub edgeHub)
        {
            const int ConnectionPoolSize = 10;
            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey(connectionStringConfigKey);
            string deviceId = ConnectionStringHelper.GetDeviceId(deviceConnectionString);
            string iotHubHostName = ConnectionStringHelper.GetHostName(deviceConnectionString);
            string sasKey = ConnectionStringHelper.GetSharedAccessKey(deviceConnectionString);
            var converters = new MessageConverterProvider(
                new Dictionary<Type, IMessageConverter>()
                {
                    { typeof(Message), new DeviceClientMessageConverter() },
                    { typeof(Twin), new TwinMessageConverter() },
                    { typeof(TwinCollection), new TwinCollectionMessageConverter() }
                });

            var credentialsCache = Mock.Of<ICredentialsCache>();
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(
                converters,
                ConnectionPoolSize,
                new ClientProvider(),
                Option.None<UpstreamProtocol>(),
                Mock.Of<Util.ITokenProvider>(),
                Mock.Of<IDeviceScopeIdentitiesCache>(),
                credentialsCache,
                Mock.Of<IIdentity>(i => i.Id == $"{deviceId}/$edgeHub"),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                Option.None<IWebProxy>());
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            var clientTokenProvider = new ClientTokenProvider(new SharedAccessKeySignatureProvider(sasKey), iotHubHostName, deviceId, TimeSpan.FromHours(1));
            string token = await clientTokenProvider.GetTokenAsync(Option.None<TimeSpan>());
            var deviceIdentity = new DeviceIdentity(iotHubHostName, deviceId);
            var clientCredentials = new TokenCredentials(deviceIdentity, token, string.Empty, false);

            Try<ICloudConnection> cloudConnection = await cloudConnectionProvider.Connect(clientCredentials, (_, __) => { });
            Assert.True(cloudConnection.Success);
            Assert.True(cloudConnection.Value.IsActive);
            Assert.True(cloudConnection.Value.CloudProxy.HasValue);
            return cloudConnection.Value.CloudProxy.OrDefault();
        }
    }
}
