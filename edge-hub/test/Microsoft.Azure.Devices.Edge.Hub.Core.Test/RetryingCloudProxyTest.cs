// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    [Unit]
    public class RetryingCloudProxyTest
    {
        [Fact]
        public async Task TestSendMessages()
        {
            // Arrange
            const string id = "id1";
            var identity = Mock.Of<IIdentity>(i => i.Id == id);
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionMessageConverter = new TwinCollectionMessageConverter();
            var messageConverterProvider = new MessageConverterProvider(
                new Dictionary<Type, IMessageConverter>()
                {
                    { typeof(Message), new DeviceClientMessageConverter() },
                    { typeof(Shared.Twin), twinMessageConverter },
                    { typeof(TwinCollection), twinCollectionMessageConverter }
                });

            var edgeHubTokenProvider = new Mock<ITokenProvider>();

            var client = new ThrowingClient();

            var clientProvider = new Mock<IClientProvider>();
            clientProvider.Setup(c => c.Create(identity, edgeHubTokenProvider.Object, It.IsAny<ITransportSettings[]>()))
                .Returns(client);

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(id, false))
                .ReturnsAsync(Option.Some(
                                  new ServiceIdentity(
                                      id, "dummy", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("foo", "bar")), ServiceIdentityStatus.Enabled)));

            var edgeHubIdentity = Mock.Of<IIdentity>();

            var productInfoStore = new Mock<IProductInfoStore>();
            productInfoStore.Setup(p => p.GetEdgeProductInfo(id))
                .ReturnsAsync("ProdInfo1");

            var identityProvider = new Mock<IIdentityProvider>();
            identityProvider.Setup(i => i.Create(id)).Returns(identity);

            var credentialsCache = new Mock<ICredentialsCache>();
            var edgeHub = new Mock<IEdgeHub>();

            var connectionProvider = new CloudConnectionProvider(
                messageConverterProvider,
                1,
                clientProvider.Object,
                Option.None<UpstreamProtocol>(),
                edgeHubTokenProvider.Object,
                deviceScopeIdentitiesCache.Object,
                credentialsCache.Object,
                edgeHubIdentity,
                TimeSpan.FromMinutes(10),
                false,
                TimeSpan.FromMinutes(10),
                Option.None<IWebProxy>(),
                productInfoStore.Object);
            connectionProvider.BindEdgeHub(edgeHub.Object);

            var connectionManager = new ConnectionManager(connectionProvider, credentialsCache.Object, identityProvider.Object);
            var messagesToSend = new List<IMessage>();
            for (int i = 0; i < 10; i++)
            {
                var message = new EdgeMessage.Builder(new byte[i])
                    .SetSystemProperties(new Dictionary<string, string>()
                    {
                        [SystemProperties.MessageId] = i.ToString()
                    })
                    .Build();
                messagesToSend.Add(message);
            }

            // Act
            Option<ICloudProxy> cloudProxyOption = await connectionManager.GetCloudConnection(id);

            // Assert
            Assert.True(cloudProxyOption.HasValue);
            ICloudProxy cloudProxy = cloudProxyOption.OrDefault();
            Assert.True(cloudProxy.IsActive);

            // Act
            foreach (IMessage message in messagesToSend)
            {
                await cloudProxy.SendMessageAsync(message);
            }

            // Assert
            Assert.Equal(messagesToSend.Count, client.ReceivedMessages.Count);
            Assert.Equal(6, client.InitializationsCount);
            Assert.True(client.IsActive);

            IEnumerable<string> expectedMessageIds = messagesToSend.Select(m => m.SystemProperties[SystemProperties.MessageId]);
            IEnumerable<string> receivedMessageIds = client.ReceivedMessages.Select(m => m.MessageId);
            Assert.Equal(expectedMessageIds, receivedMessageIds);
        }

        [Fact]
        public async Task TestGetTwin()
        {
            // Arrange
            const string id = "id1";
            var identity = Mock.Of<IIdentity>(i => i.Id == id);
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionMessageConverter = new TwinCollectionMessageConverter();
            var messageConverterProvider = new MessageConverterProvider(
                new Dictionary<Type, IMessageConverter>()
                {
                    { typeof(Message), new DeviceClientMessageConverter() },
                    { typeof(Shared.Twin), twinMessageConverter },
                    { typeof(TwinCollection), twinCollectionMessageConverter }
                });

            var edgeHubTokenProvider = new Mock<ITokenProvider>();

            var client = new ThrowingClient();

            var clientProvider = new Mock<IClientProvider>();
            clientProvider.Setup(c => c.Create(identity, edgeHubTokenProvider.Object, It.IsAny<ITransportSettings[]>()))
                .Returns(client);

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(id, false))
                .ReturnsAsync(Option.Some(
                                  new ServiceIdentity(
                                      id, "dummy", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("foo", "bar")), ServiceIdentityStatus.Enabled)));

            var edgeHubIdentity = Mock.Of<IIdentity>();

            var productInfoStore = new Mock<IProductInfoStore>();
            productInfoStore.Setup(p => p.GetEdgeProductInfo(id))
                .ReturnsAsync("ProdInfo1");

            var identityProvider = new Mock<IIdentityProvider>();
            identityProvider.Setup(i => i.Create(id)).Returns(identity);

            var credentialsCache = new Mock<ICredentialsCache>();
            var edgeHub = new Mock<IEdgeHub>();

            var connectionProvider = new CloudConnectionProvider(
                messageConverterProvider,
                1,
                clientProvider.Object,
                Option.None<UpstreamProtocol>(),
                edgeHubTokenProvider.Object,
                deviceScopeIdentitiesCache.Object,
                credentialsCache.Object,
                edgeHubIdentity,
                TimeSpan.FromMinutes(10),
                false,
                TimeSpan.FromMinutes(10),
                Option.None<IWebProxy>(),
                productInfoStore.Object);
            connectionProvider.BindEdgeHub(edgeHub.Object);

            var connectionManager = new ConnectionManager(connectionProvider, credentialsCache.Object, identityProvider.Object);

            // Act
            Option<ICloudProxy> cloudProxyOption = await connectionManager.GetCloudConnection(id);

            // Assert
            Assert.True(cloudProxyOption.HasValue);
            ICloudProxy cloudProxy = cloudProxyOption.OrDefault();
            Assert.True(cloudProxy.IsActive);

            // Act
            for (int i = 0; i < 10; i++)
            {
                IMessage twin = await cloudProxy.GetTwinAsync();
                Assert.NotNull(twin);
            }

            // Assert
            Assert.Equal(6, client.InitializationsCount);
            Assert.True(client.IsActive);
        }

        class ThrowingClient : IClient
        {
            readonly object stateLock = new object();
            readonly List<Message> receivedMessages = new List<Message>();
            readonly List<string> receivedProductInfos = new List<string>();

            int operationCount;
            bool isActive = true;
            int initializationsCount;

            public IList<Message> ReceivedMessages => this.receivedMessages;

            public IList<string> ReceivedProductInfos => this.receivedProductInfos;

            public int InitializationsCount => this.initializationsCount;

            public void Dispose() => throw new NotImplementedException();

            public bool IsActive => this.isActive;

            public async Task<Shared.Twin> GetTwinAsync()
            {
                this.UpdateOperationCounter();
                await Task.CompletedTask;
                return new Shared.Twin();
            }

            public async Task SendEventAsync(Message message)
            {
                this.UpdateOperationCounter();
                await Task.CompletedTask;
                this.receivedMessages.Add(message);
            }

            public async Task SendEventBatchAsync(IEnumerable<Message> messages)
            {
                this.UpdateOperationCounter();
                await Task.CompletedTask;
                this.receivedMessages.AddRange(messages);
            }

            public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => throw new NotImplementedException();

            public Task CompleteAsync(string messageId) => throw new NotImplementedException();

            public Task AbandonAsync(string messageId) => throw new NotImplementedException();

            public Task SetMethodDefaultHandlerAsync(MethodCallback methodHandler, object userContext) => Task.CompletedTask;

            public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyUpdates1, object userContext) => Task.CompletedTask;

            public void SetOperationTimeoutInMilliseconds(uint defaultOperationTimeoutMilliseconds)
            {
            }

            public void SetConnectionStatusChangedHandler(ConnectionStatusChangesHandler handler)
            {
            }

            public void SetProductInfo(string productInfo)
            {
                this.receivedProductInfos.Add(productInfo);
            }

            public Task OpenAsync()
            {
                lock (this.stateLock)
                {
                    this.initializationsCount++;
                    this.isActive = true;
                }

                return Task.CompletedTask;
            }

            public Task CloseAsync()
            {
                lock (this.stateLock)
                {
                    this.isActive = false;
                }

                return Task.CompletedTask;
            }

            public Task RejectAsync(string messageId) => throw new NotImplementedException();

            public Task<Message> ReceiveAsync(TimeSpan receiveMessageTimeout) => throw new NotImplementedException();

            void UpdateOperationCounter()
            {
                lock (this.stateLock)
                {
                    if ((this.operationCount++) % 3 == 0)
                    {
                        throw new ObjectDisposedException("Object is disposed");
                    }
                }
            }
        }
    }
}
