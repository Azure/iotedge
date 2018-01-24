// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Mqtt.Packets;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
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

        readonly IEntityStore<string, SessionState> entityStore = new StoreProvider(new InMemoryDbStoreProvider()).GetEntityStore<string, SessionState>(Core.Constants.SessionStorePartitionKey);

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

            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "deviceId");
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

            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "deviceId");
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
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "deviceId");

            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);

            Task<ISessionState> task = sessionProvider.GetAsync(identity);

            Assert.Null(task.Result);
        }

        [Fact]
        [Unit]
        public void TestGetAsync_DeleteSession()
        {
            var connectionManager = new Mock<IConnectionManager>();
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "deviceId");

            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            Task task = sessionProvider.DeleteAsync(identity, It.IsAny<ISessionState>());

            Assert.True(task.IsCompleted);
        }

        [Fact]
        [Unit]
        public async Task TestSetAsync_AddedMethodSubscription_ShouldSaveToStore()
        {
            var cloudProxy = new Mock<ICloudProxy>();
            Option<ICloudProxy> cloudProxyOption = Option.Some(cloudProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(cm => cm.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxyOption);

            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "deviceId");
            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            await sessionProvider.SetAsync(identity, sessionState);

            ISessionState storedSession = await sessionProvider.GetAsync(identity);
            Assert.NotNull(storedSession);
            cloudProxy.Verify(x => x.SetupCallMethodAsync(), Times.Once);

            // clean up
            await sessionProvider.DeleteAsync(identity, sessionState);
        }

        [Fact]
        [Unit]
        public async Task TestSetAsync_AddedMethodSubscription_TransientSession_ShouldNotSaveToStore()
        {
            var cloudProxy = new Mock<ICloudProxy>();
            Option<ICloudProxy> cloudProxyOption = Option.Some(cloudProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(cm => cm.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxyOption);

            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "deviceId");
            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            var sessionState = new SessionState(true);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            await sessionProvider.SetAsync(identity, sessionState);

            ISessionState storedSession = await sessionProvider.GetAsync(identity);
            Assert.Null(storedSession);
            cloudProxy.Verify(x => x.SetupCallMethodAsync(), Times.Once);
        }

        [Fact]
        [Unit]
        public async Task TestSetAsync_RemovedSubscription_ShouldUpdateStore()
        {
            var cloudProxy = new Mock<ICloudProxy>();
            Option<ICloudProxy> cloudProxyOption = Option.Some(cloudProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(cm => cm.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxyOption);

            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "deviceId");
            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            await sessionProvider.SetAsync(identity, sessionState);

            sessionState.RemoveSubscription(MethodPostTopicPrefix);
            await sessionProvider.SetAsync(identity, sessionState);

            ISessionState storedSession = await sessionProvider.GetAsync(identity);
            Assert.NotNull(storedSession);
            Assert.Null(storedSession.Subscriptions.SingleOrDefault(p => p.TopicFilter == MethodPostTopicPrefix));
            cloudProxy.Verify(x => x.RemoveCallMethodAsync(), Times.Once);

            // clean up
            await sessionProvider.DeleteAsync(identity, sessionState);
        }

        [Fact]
        [Unit]
        public async Task TestGetAsync_DeleteSession_DeleteFromStore()
        {
            var connectionManager = new Mock<IConnectionManager>();
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "deviceId");

            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            var sessionState = new SessionState(false);
            await sessionProvider.SetAsync(identity, sessionState);

            await sessionProvider.DeleteAsync(identity, It.IsAny<ISessionState>());

            ISessionState storedSession = await sessionProvider.GetAsync(identity);
            Assert.Null(storedSession);
        }

        [Fact]
        [Unit]
        public async Task TestPersistence()
        {
            var cloudProxy = new Mock<ICloudProxy>();
            Option<ICloudProxy> cloudProxyOption = Option.Some(cloudProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.SetupSequence(cm => cm.GetCloudConnection(It.IsAny<string>()))
                .Returns(Option.None<ICloudProxy>())
                .Returns(cloudProxyOption)
                .Returns(cloudProxyOption);

            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "deviceId");
            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager.Object, this.entityStore);
            ISessionState sessionState = await sessionProvider.GetAsync(identity);
            Assert.Null(sessionState);

            sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix, QualityOfService.AtLeastOnce);
            sessionState.AddOrUpdateSubscription(SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix, QualityOfService.AtMostOnce);
            sessionState.RemoveSubscription(SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix);
            await sessionProvider.SetAsync(identity, sessionState);

            cloudProxy.Verify(x => x.SetupCallMethodAsync(), Times.Never);
            cloudProxy.Verify(x => x.StartListening(), Times.Never);
            cloudProxy.Verify(x => x.RemoveDesiredPropertyUpdatesAsync(), Times.Never);

            ISessionState storedSession = await sessionProvider.GetAsync(identity);
            Assert.NotNull(storedSession);

            var retrievedSessionState = storedSession as SessionState;
            Assert.NotNull(retrievedSessionState);
            Assert.Equal(2, retrievedSessionState.Subscriptions.Count);
            Assert.Equal(3, retrievedSessionState.SubscriptionRegistrations.Count);            
            Assert.True(retrievedSessionState.SubscriptionRegistrations.ContainsKey(SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix));
            Assert.True(retrievedSessionState.SubscriptionRegistrations.ContainsKey(SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix));
            Assert.True(retrievedSessionState.SubscriptionRegistrations[SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix]);
            Assert.True(retrievedSessionState.SubscriptionRegistrations[SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix]);
            Assert.True(retrievedSessionState.SubscriptionRegistrations.ContainsKey(SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix));
            Assert.False(retrievedSessionState.SubscriptionRegistrations[SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix]);
            ISubscription c2DSubscription = retrievedSessionState.Subscriptions.FirstOrDefault(s => s.TopicFilter == SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix);
            Assert.NotNull(c2DSubscription);
            Assert.Equal(QualityOfService.AtLeastOnce, c2DSubscription.QualityOfService);
            Assert.True(DateTime.UtcNow - c2DSubscription.CreationTime < TimeSpan.FromMinutes(2));
            ISubscription methodSubscription = retrievedSessionState.Subscriptions.FirstOrDefault(s => s.TopicFilter == SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix);
            Assert.NotNull(methodSubscription);
            Assert.Equal(QualityOfService.AtMostOnce, methodSubscription.QualityOfService);
            Assert.True(DateTime.UtcNow - methodSubscription.CreationTime < TimeSpan.FromMinutes(2));

            await sessionProvider.SetAsync(identity, sessionState);

            cloudProxy.Verify(x => x.SetupCallMethodAsync(), Times.Once);
            cloudProxy.Verify(x => x.StartListening(), Times.Once);
            cloudProxy.Verify(x => x.RemoveDesiredPropertyUpdatesAsync(), Times.Once);

            storedSession = await sessionProvider.GetAsync(identity);
            Assert.NotNull(storedSession);

            retrievedSessionState = storedSession as SessionState;
            Assert.NotNull(retrievedSessionState);
            Assert.Equal(2, retrievedSessionState.Subscriptions.Count);
            Assert.Equal(3, retrievedSessionState.SubscriptionRegistrations.Count);
            Assert.True(retrievedSessionState.SubscriptionRegistrations.ContainsKey(SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix));
            Assert.True(retrievedSessionState.SubscriptionRegistrations.ContainsKey(SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix));
            Assert.True(retrievedSessionState.SubscriptionRegistrations[SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix]);
            Assert.True(retrievedSessionState.SubscriptionRegistrations[SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix]);
            Assert.True(retrievedSessionState.SubscriptionRegistrations.ContainsKey(SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix));
            Assert.False(retrievedSessionState.SubscriptionRegistrations[SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix]);
            c2DSubscription = retrievedSessionState.Subscriptions.FirstOrDefault(s => s.TopicFilter == SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix);
            Assert.NotNull(c2DSubscription);
            Assert.Equal(QualityOfService.AtLeastOnce, c2DSubscription.QualityOfService);
            Assert.True(DateTime.UtcNow - c2DSubscription.CreationTime < TimeSpan.FromMinutes(2));
            methodSubscription = retrievedSessionState.Subscriptions.FirstOrDefault(s => s.TopicFilter == SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix);
            Assert.NotNull(methodSubscription);
            Assert.Equal(QualityOfService.AtMostOnce, methodSubscription.QualityOfService);
            Assert.True(DateTime.UtcNow - methodSubscription.CreationTime < TimeSpan.FromMinutes(2));

            await sessionProvider.SetAsync(identity, sessionState);

            cloudProxy.Verify(x => x.SetupCallMethodAsync(), Times.Exactly(2));
            cloudProxy.Verify(x => x.StartListening(), Times.Exactly(2));
            cloudProxy.Verify(x => x.RemoveDesiredPropertyUpdatesAsync(), Times.Exactly(2));
        }

        [Fact]
        [Unit]
        public async Task TestConnectionEstablishedReenableSubscriptions()
        {
            string deviceId = "deviceId";            

            var cloudProxyMock = new Mock<ICloudProxy>();
            cloudProxyMock.SetupGet(cp => cp.IsActive).Returns(true);

            var cloudConnectionMock = new Mock<ICloudConnection>();
            cloudConnectionMock.SetupGet(dp => dp.IsActive).Returns(true);
            cloudConnectionMock.SetupGet(dp => dp.CloudProxy).Returns(Option.Some(cloudProxyMock.Object));
            cloudConnectionMock.Setup(c => c.CreateOrUpdateAsync(It.IsAny<IIdentity>()))
                .ReturnsAsync(cloudProxyMock.Object);

            Action<string, CloudConnectionStatus> connectionChangeCallback = (_, __) => { };
            var cloudConnectionProvider = new Mock<ICloudConnectionProvider>();
            cloudConnectionProvider.Setup(c => c.Connect(It.IsAny<IIdentity>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .Callback<IIdentity, Action<string, CloudConnectionStatus>>((id, cb) => connectionChangeCallback = cb)
                .ReturnsAsync(() => Try.Success(cloudConnectionMock.Object));

            var protocolGatewayIdentity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == deviceId);
            var edgeIdentity = Mock.Of<IIdentity>(i => i.Id == deviceId);

            var connectionManager = new ConnectionManager(cloudConnectionProvider.Object);
            await connectionManager.CreateCloudConnectionAsync(edgeIdentity);
            
            var sessionProvider = new SessionStateStoragePersistenceProvider(connectionManager, this.entityStore);
            ISessionState sessionState = await sessionProvider.GetAsync(protocolGatewayIdentity);
            Assert.Null(sessionState);

            sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix, QualityOfService.AtLeastOnce);
            sessionState.AddOrUpdateSubscription(SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix, QualityOfService.AtMostOnce);
            sessionState.RemoveSubscription(SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix);
            await sessionProvider.SetAsync(protocolGatewayIdentity, sessionState);

            cloudProxyMock.Verify(x => x.SetupCallMethodAsync(), Times.Once);
            cloudProxyMock.Verify(x => x.StartListening(), Times.Once);
            cloudProxyMock.Verify(x => x.RemoveDesiredPropertyUpdatesAsync(), Times.Once);

            ISessionState storedSession = await sessionProvider.GetAsync(protocolGatewayIdentity);
            Assert.NotNull(storedSession);

            var retrievedSessionState = storedSession as SessionState;
            Assert.NotNull(retrievedSessionState);
            Assert.Equal(2, retrievedSessionState.Subscriptions.Count);
            Assert.Equal(3, retrievedSessionState.SubscriptionRegistrations.Count);
            Assert.True(retrievedSessionState.SubscriptionRegistrations.ContainsKey(SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix));
            Assert.True(retrievedSessionState.SubscriptionRegistrations[SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix]);
            Assert.True(retrievedSessionState.SubscriptionRegistrations.ContainsKey(SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix));            
            Assert.True(retrievedSessionState.SubscriptionRegistrations[SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix]);
            Assert.True(retrievedSessionState.SubscriptionRegistrations.ContainsKey(SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix));
            Assert.False(retrievedSessionState.SubscriptionRegistrations[SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix]);
            ISubscription c2DSubscription = retrievedSessionState.Subscriptions.FirstOrDefault(s => s.TopicFilter == SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix);
            Assert.NotNull(c2DSubscription);
            Assert.Equal(QualityOfService.AtLeastOnce, c2DSubscription.QualityOfService);
            Assert.True(DateTime.UtcNow - c2DSubscription.CreationTime < TimeSpan.FromMinutes(2));
            ISubscription methodSubscription = retrievedSessionState.Subscriptions.FirstOrDefault(s => s.TopicFilter == SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix);
            Assert.NotNull(methodSubscription);
            Assert.Equal(QualityOfService.AtMostOnce, methodSubscription.QualityOfService);
            Assert.True(DateTime.UtcNow - methodSubscription.CreationTime < TimeSpan.FromMinutes(2));

            await sessionProvider.SetAsync(protocolGatewayIdentity, sessionState);

            cloudProxyMock.Verify(x => x.SetupCallMethodAsync(), Times.Exactly(2));
            cloudProxyMock.Verify(x => x.StartListening(), Times.Exactly(2));
            cloudProxyMock.Verify(x => x.RemoveDesiredPropertyUpdatesAsync(), Times.Exactly(2));

            connectionChangeCallback.Invoke(deviceId, CloudConnectionStatus.ConnectionEstablished);

            cloudProxyMock.Verify(x => x.SetupCallMethodAsync(), Times.Exactly(3));
            cloudProxyMock.Verify(x => x.StartListening(), Times.Exactly(3));
            cloudProxyMock.Verify(x => x.RemoveDesiredPropertyUpdatesAsync(), Times.Exactly(3));
        }        
    }
}
