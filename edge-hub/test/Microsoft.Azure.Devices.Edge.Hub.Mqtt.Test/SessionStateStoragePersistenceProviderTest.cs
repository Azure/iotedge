// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System.Linq;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Mqtt.Packets;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Moq;
    using Xunit;
    using IProtocolgatewayDeviceIdentity = Microsoft.Azure.Devices.ProtocolGateway.Identity.IDeviceIdentity;

    public class SessionStateStoragePersistenceProviderTest
    {
        const string MethodPostTopicPrefix = "$iothub/methods/POST/";
        readonly IEntityStore<string, ISessionState> entityStore = new StoreProvider(new InMemoryDbStoreProvider()).GetEntityStore<string, ISessionState>(Core.Constants.SessionStorePartitionKey);

        [Fact]
        [Unit]
        public void TestCreate_ShouldReturn_Session()
        {
            IConnectionManager connectionManager = new Mock<IConnectionManager>().Object;
            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager, this.entityStore);
            ISessionState session = sessionProvider.Create(true);

            Assert.NotNull(session);
            Assert.False(session.IsTransient);
        }

        [Fact]
        [Unit]
        public void TestSetAsync_AddedMethodSubscription_ShouldComplete()
        {
            var cloudProxy = new Mock<ICloudProxy>();
            Option<ICloudProxy> cloudProxyOption = Option.Some(cloudProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(cm => cm.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxyOption);

            IProtocolgatewayDeviceIdentity identity = new Mock<IProtocolgatewayDeviceIdentity>().Object;
            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            Task setTask = sessionProvider.SetAsync(identity, sessionState);

            Assert.True(setTask.IsCompleted);
            cloudProxy.Verify(x => x.SetupCallMethodAsync(), Times.Once);
        }

        [Fact]
        [Unit]
        public void TestSetAsync_RemovedSubscription_ShouldComplete()
        {
            var cloudProxy = new Mock<ICloudProxy>();
            Option<ICloudProxy> cloudProxyOption = Option.Some(cloudProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(cm => cm.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxyOption);

            IProtocolgatewayDeviceIdentity identity = new Mock<IProtocolgatewayDeviceIdentity>().Object;
            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            var sessionState = new SessionState(false);
            sessionState.RemoveSubscription(MethodPostTopicPrefix);
            Task setTask = sessionProvider.SetAsync(identity, sessionState);

            Assert.True(setTask.IsCompleted);
            cloudProxy.Verify(x => x.RemoveCallMethodAsync(), Times.Once);
        }

        [Fact]
        [Unit]
        public void TestGetAsync_NoSessionFound_ShouldReturnNull()
        {
            var connectionManager = new Mock<IConnectionManager>();
            var identity = new Mock<IProtocolgatewayDeviceIdentity>();
            identity.Setup(p => p.Id).Returns("device-id");

            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);

            Task<ISessionState> task = sessionProvider.GetAsync(identity.Object);

            Assert.Null(task.Result);
        }

        [Fact]
        [Unit]
        public void TestGetAsync_DeleteSession()
        {
            var connectionManager = new Mock<IConnectionManager>();
            var identity = new Mock<IProtocolgatewayDeviceIdentity>();
            identity.Setup(p => p.Id).Returns("device-id");

            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            Task task = sessionProvider.DeleteAsync(identity.Object, It.IsAny<ISessionState>());

            Assert.True(task.IsCompleted);
        }

        [Fact]
        [Unit]
        public async void TestSetAsync_AddedMethodSubscription_ShouldSaveToStore()
        {
            var cloudProxy = new Mock<ICloudProxy>();
            Option<ICloudProxy> cloudProxyOption = Option.Some(cloudProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(cm => cm.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxyOption);

            var identity = new Mock<IProtocolgatewayDeviceIdentity>();
            identity.Setup(p => p.Id).Returns("device-id");
            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            await sessionProvider.SetAsync(identity.Object, sessionState);

            ISessionState storedSession = await sessionProvider.GetAsync(identity.Object);
            Assert.NotNull(storedSession);
            cloudProxy.Verify(x => x.SetupCallMethodAsync(), Times.Once);

            // clean up
            await sessionProvider.DeleteAsync(identity.Object, sessionState);
        }

        [Fact]
        [Unit]
        public async void TestSetAsync_AddedMethodSubscription_TransientSession_ShouldNotSaveToStore()
        {
            var cloudProxy = new Mock<ICloudProxy>();
            Option<ICloudProxy> cloudProxyOption = Option.Some(cloudProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(cm => cm.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxyOption);

            var identity = new Mock<IProtocolgatewayDeviceIdentity>();
            identity.Setup(p => p.Id).Returns("device-id");
            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            var sessionState = new SessionState(true);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            await sessionProvider.SetAsync(identity.Object, sessionState);

            ISessionState storedSession = await sessionProvider.GetAsync(identity.Object);
            Assert.Null(storedSession);
            cloudProxy.Verify(x => x.SetupCallMethodAsync(), Times.Once);
        }

        [Fact]
        [Unit]
        public async void TestSetAsync_RemovedSubscription_ShouldUpdateStore()
        {
            var cloudProxy = new Mock<ICloudProxy>();
            Option<ICloudProxy> cloudProxyOption = Option.Some(cloudProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(cm => cm.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxyOption);

            var identity = new Mock<IProtocolgatewayDeviceIdentity>();
            identity.Setup(p => p.Id).Returns("device-id");
            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            await sessionProvider.SetAsync(identity.Object, sessionState);

            sessionState.RemoveSubscription(MethodPostTopicPrefix);
            await sessionProvider.SetAsync(identity.Object, sessionState);

            ISessionState storedSession = await sessionProvider.GetAsync(identity.Object);
            Assert.NotNull(storedSession);
            Assert.Null(storedSession.Subscriptions.SingleOrDefault(p => p.TopicFilter == MethodPostTopicPrefix));
            cloudProxy.Verify(x => x.RemoveCallMethodAsync(), Times.Once);

            // clean up
            await sessionProvider.DeleteAsync(identity.Object, sessionState);
        }

        [Fact]
        [Unit]
        public async void TestGetAsync_DeleteSession_DeleteFromStore()
        {
            var connectionManager = new Mock<IConnectionManager>();
            var identity = new Mock<IProtocolgatewayDeviceIdentity>();
            identity.Setup(p => p.Id).Returns("device-id");

            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            var sessionState = new SessionState(false);
            await sessionProvider.SetAsync(identity.Object, sessionState);

            await sessionProvider.DeleteAsync(identity.Object, It.IsAny<ISessionState>());

            ISessionState storedSession = await sessionProvider.GetAsync(identity.Object);
            Assert.Null(storedSession);
        }
    }
}
