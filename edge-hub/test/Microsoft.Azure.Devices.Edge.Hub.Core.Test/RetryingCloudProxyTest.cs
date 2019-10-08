// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
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
            const string Id = "id1";
            var identity = Mock.Of<IIdentity>(i => i.Id == Id);
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

            var clientWatcher = new ClientWatcher();

            var clientProvider = new Mock<IClientProvider>();
            clientProvider.Setup(c => c.Create(identity, edgeHubTokenProvider.Object, It.IsAny<ITransportSettings[]>()))
                .Returns(() => new ThrowingClient(clientWatcher, 3));

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(Id, false))
                .ReturnsAsync(
                    Option.Some(
                        new ServiceIdentity(
                            Id,
                            "dummy",
                            new List<string>(),
                            new ServiceAuthentication(new SymmetricKeyAuthentication("foo", "bar")),
                            ServiceIdentityStatus.Enabled)));

            var edgeHubIdentity = Mock.Of<IIdentity>();

            var productInfoStore = new Mock<IProductInfoStore>();
            productInfoStore.Setup(p => p.GetEdgeProductInfo(Id))
                .ReturnsAsync("ProdInfo1");

            var identityProvider = new Mock<IIdentityProvider>();
            identityProvider.Setup(i => i.Create(Id)).Returns(identity);

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
                    .SetSystemProperties(
                        new Dictionary<string, string>()
                        {
                            [SystemProperties.MessageId] = i.ToString()
                        })
                    .Build();
                messagesToSend.Add(message);
            }

            // Act
            Option<ICloudProxy> cloudProxyOption = await connectionManager.GetCloudConnection(Id);

            // Assert
            Assert.True(cloudProxyOption.HasValue);
            ICloudProxy cloudProxy = cloudProxyOption.OrDefault();
            Assert.True(cloudProxy.IsActive);

            // Act
            await RunSendMessages(cloudProxy, messagesToSend);

            // Assert
            Assert.Equal(messagesToSend.Count, clientWatcher.ReceivedMessages.Count());
            Assert.Equal(5, clientWatcher.OpenAsyncCount);
            Assert.True(cloudProxy.IsActive);

            IEnumerable<string> expectedMessageIds = messagesToSend.Select(m => m.SystemProperties[SystemProperties.MessageId]);
            IEnumerable<string> receivedMessageIds = clientWatcher.ReceivedMessages.Select(m => m.MessageId);
            Assert.Equal(expectedMessageIds, receivedMessageIds);
        }

        [Fact]
        public async Task TestGetTwin()
        {
            // Arrange
            const string Id = "id1";
            var identity = Mock.Of<IIdentity>(i => i.Id == Id);
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

            var clientWatcher = new ClientWatcher();

            var clientProvider = new Mock<IClientProvider>();
            clientProvider.Setup(c => c.Create(identity, edgeHubTokenProvider.Object, It.IsAny<ITransportSettings[]>()))
                .Returns(() => new ThrowingClient(clientWatcher, 3));

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(Id, false))
                .ReturnsAsync(
                    Option.Some(
                        new ServiceIdentity(
                            Id,
                            "dummy",
                            new List<string>(),
                            new ServiceAuthentication(new SymmetricKeyAuthentication("foo", "bar")),
                            ServiceIdentityStatus.Enabled)));

            var edgeHubIdentity = Mock.Of<IIdentity>();

            var productInfoStore = new Mock<IProductInfoStore>();
            productInfoStore.Setup(p => p.GetEdgeProductInfo(Id))
                .ReturnsAsync("ProdInfo1");

            var identityProvider = new Mock<IIdentityProvider>();
            identityProvider.Setup(i => i.Create(Id)).Returns(identity);

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
            Option<ICloudProxy> cloudProxyOption = await connectionManager.GetCloudConnection(Id);

            // Assert
            Assert.True(cloudProxyOption.HasValue);
            ICloudProxy cloudProxy = cloudProxyOption.OrDefault();
            Assert.True(cloudProxy.IsActive);

            // Act
            await RunGetTwin(cloudProxy, 10);

            // Assert
            Assert.Equal(5, clientWatcher.OpenAsyncCount);
            Assert.True(cloudProxy.IsActive);
            Assert.Equal(10, clientWatcher.GetTwinCount);
        }

        [Fact]
        public async Task TestMultipleOperations()
        {
            // Arrange
            const string Id = "id1";
            var identity = Mock.Of<IIdentity>(i => i.Id == Id);
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionMessageConverter = new TwinCollectionMessageConverter();
            var messageConverterProvider = new MessageConverterProvider(
                new Dictionary<Type, IMessageConverter>
                {
                    { typeof(Message), new DeviceClientMessageConverter() },
                    { typeof(Shared.Twin), twinMessageConverter },
                    { typeof(TwinCollection), twinCollectionMessageConverter }
                });

            var edgeHubTokenProvider = new Mock<ITokenProvider>();

            var clientWatcher = new ClientWatcher();

            var clientProvider = new Mock<IClientProvider>();
            clientProvider.Setup(c => c.Create(identity, edgeHubTokenProvider.Object, It.IsAny<ITransportSettings[]>()))
                .Returns(() => new ThrowingClient(clientWatcher, 15));

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(Id, false))
                .ReturnsAsync(
                    Option.Some(
                        new ServiceIdentity(
                            Id,
                            "dummy",
                            new List<string>(),
                            new ServiceAuthentication(new SymmetricKeyAuthentication("foo", "bar")),
                            ServiceIdentityStatus.Enabled)));

            var edgeHubIdentity = Mock.Of<IIdentity>();

            var productInfoStore = new Mock<IProductInfoStore>();
            productInfoStore.Setup(p => p.GetEdgeProductInfo(Id))
                .ReturnsAsync("ProdInfo1");

            var identityProvider = new Mock<IIdentityProvider>();
            identityProvider.Setup(i => i.Create(Id)).Returns(identity);

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

            async Task<ICloudProxy> GetCloudProxy(IConnectionManager cm)
            {
                // Act
                Option<ICloudProxy> cloudProxyOption = await cm.GetCloudConnection(Id);

                // Assert
                Assert.True(cloudProxyOption.HasValue);
                ICloudProxy cloudProxy = cloudProxyOption.OrDefault();
                Assert.True(cloudProxy.IsActive);
                return cloudProxy;
            }

            var messagesToSend = new List<IMessage>();
            for (int i = 0; i < 60; i++)
            {
                var message = new EdgeMessage.Builder(new byte[i])
                    .SetSystemProperties(
                        new Dictionary<string, string>()
                        {
                            [SystemProperties.MessageId] = i.ToString()
                        })
                    .Build();
                messagesToSend.Add(message);
            }

            // Act
            ICloudProxy cloudProxy1 = await GetCloudProxy(connectionManager);
            ICloudProxy cloudProxy2 = await GetCloudProxy(connectionManager);
            ICloudProxy cloudProxy3 = await GetCloudProxy(connectionManager);

            // Act
            var tasks = new[]
            {
                RunGetTwin(cloudProxy1, 10),
                RunGetTwin(cloudProxy2, 30),
                RunGetTwin(cloudProxy3, 10),
                RunSendMessages(cloudProxy1, messagesToSend.Take(20), 2),
                RunSendMessages(cloudProxy2, messagesToSend.Skip(20).Take(10)),
                RunSendMessages(cloudProxy3, messagesToSend.Skip(30).Take(30), 3)
            };
            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(50, clientWatcher.GetTwinCount);
            List<string> expectedMessageIds = messagesToSend
                .Select(m => m.SystemProperties[SystemProperties.MessageId])
                .OrderBy(s => s)
                .ToList();
            List<string> receivedMessageIds = clientWatcher.ReceivedMessages
                .Select(m => m.MessageId)
                .OrderBy(s => s)
                .ToList();
            Assert.Equal(expectedMessageIds.Count, receivedMessageIds.Count);
            Assert.Equal(expectedMessageIds, receivedMessageIds);
        }

        static async Task RunSendMessages(ICloudProxy cloudProxy, IEnumerable<IMessage> messages, int batchSize = 1)
        {
            if (batchSize == 1)
            {
                foreach (IMessage message in messages)
                {
                    await cloudProxy.SendMessageAsync(message);
                }
            }
            else
            {
                var batches = messages.Batch(batchSize);
                foreach (IEnumerable<IMessage> batch in batches)
                {
                    await cloudProxy.SendMessageBatchAsync(batch);
                }
            }
        }

        static async Task RunGetTwin(ICloudProxy cloudProxy, int count)
        {
            for (int i = 0; i < count; i++)
            {
                IMessage twin = await cloudProxy.GetTwinAsync();
                Assert.NotNull(twin);
            }
        }

        class ClientWatcher
        {
            readonly List<Message> receivedMessages = new List<Message>();
            readonly List<string> receivedProductInfos = new List<string>();

            int getTwinCount;
            int openAsyncCount;

            public IEnumerable<Message> ReceivedMessages => this.receivedMessages;

            public IEnumerable<string> ReceivedProductInfos => this.receivedProductInfos;

            public int OpenAsyncCount => this.openAsyncCount;

            public int GetTwinCount => this.getTwinCount;

            public void IncrementGetTwinCount() => Interlocked.Increment(ref this.getTwinCount);

            public void IncrementOpenAsyncCount() => Interlocked.Increment(ref this.openAsyncCount);

            public void AddReceivedMessage(Message message)
            {
                lock (this.receivedMessages)
                {
                    this.receivedMessages.Add(message);
                }
            }

            public void AddReceivedMessages(IEnumerable<Message> messages)
            {
                lock (this.receivedMessages)
                {
                    this.receivedMessages.AddRange(messages);
                }
            }

            public void AddReceivedProductInfo(string productInfo)
            {
                lock (this.receivedProductInfos)
                {
                    this.receivedProductInfos.Add(productInfo);
                }
            }
        }

        class ThrowingClient : IClient
        {
            readonly object stateLock = new object();
            readonly int throwAfterOperationsCount;
            readonly Random random = new Random();
            readonly ClientWatcher clientWatcher;

            int operationCount;

            public ThrowingClient(ClientWatcher clientWatcher, int throwAfterOperationsCount)
            {
                this.clientWatcher = clientWatcher;
                this.throwAfterOperationsCount = throwAfterOperationsCount;
            }

            public bool IsActive { get; private set; }

            public void Dispose() => throw new NotImplementedException();

            public async Task<Shared.Twin> GetTwinAsync()
            {
                this.UpdateOperationCounter();
                this.clientWatcher.IncrementGetTwinCount();
                await Task.Delay(TimeSpan.FromMilliseconds(100 + this.random.Next(50)));
                return new Shared.Twin();
            }

            public async Task SendEventAsync(Message message)
            {
                this.UpdateOperationCounter();
                await Task.Delay(TimeSpan.FromMilliseconds(100 + this.random.Next(50)));
                this.clientWatcher.AddReceivedMessage(message);
            }

            public async Task SendEventBatchAsync(IEnumerable<Message> messages)
            {
                this.UpdateOperationCounter();
                await Task.Delay(TimeSpan.FromMilliseconds(50 + this.random.Next(50)));
                this.clientWatcher.AddReceivedMessages(messages);
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
                this.clientWatcher.AddReceivedProductInfo(productInfo);
            }

            public Task OpenAsync()
            {
                lock (this.stateLock)
                {
                    if (this.IsActive)
                    {
                        throw new InvalidOperationException("Open called when client is active.");
                    }

                    this.clientWatcher.IncrementOpenAsyncCount();
                    this.IsActive = true;
                }

                return Task.CompletedTask;
            }

            public Task CloseAsync()
            {
                lock (this.stateLock)
                {
                    this.IsActive = false;
                }

                return Task.CompletedTask;
            }

            public Task RejectAsync(string messageId) => throw new NotImplementedException();

            public Task<Message> ReceiveAsync(TimeSpan receiveMessageTimeout) => throw new NotImplementedException();

            void UpdateOperationCounter()
            {
                lock (this.stateLock)
                {
                    if (++this.operationCount % this.throwAfterOperationsCount == 0 || !this.IsActive)
                    {
                        throw new ObjectDisposedException("Object is disposed");
                    }
                }
            }
        }
    }
}
