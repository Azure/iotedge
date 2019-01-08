// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Mqtt.Packets;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Moq;
    using Xunit;
    using IProtocolgatewayDeviceIdentity = Microsoft.Azure.Devices.ProtocolGateway.Identity.IDeviceIdentity;

    public class SessionStateStoragePersistenceProviderTest
    {
        const string MethodPostTopicPrefix = "$iothub/methods/POST/";

        readonly IEntityStore<string, SessionState> entityStore = new StoreProvider(new InMemoryDbStoreProvider()).GetEntityStore<string, SessionState>(Constants.SessionStorePartitionKey);

        [Fact]
        [Unit]
        public void TestCreate_ShouldReturn_Session()
        {
            var sessionProvider = new SessionStateStoragePersistenceProvider(Mock.Of<IEdgeHub>(), this.entityStore);
            ISessionState session = sessionProvider.Create(true);

            Assert.NotNull(session);
            Assert.False(session.IsTransient);
        }

        [Fact]
        [Unit]
        public void TestSetAsync_AddedMethodSubscription_ShouldComplete()
        {
            var edgeHub = new Mock<IEdgeHub>();
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "d1");
            var sessionProvider = new SessionStateStoragePersistenceProvider(edgeHub.Object, this.entityStore);
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            Task setTask = sessionProvider.SetAsync(identity, sessionState);

            Assert.True(setTask.IsCompleted);
            edgeHub.Verify(x => x.AddSubscription("d1", DeviceSubscription.Methods), Times.Once);
        }

        [Fact]
        [Unit]
        public void TestSetAsync_RemovedSubscription_ShouldComplete()
        {
            var edgeHub = new Mock<IEdgeHub>();
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "d1");
            var sessionProvider = new SessionStateStoragePersistenceProvider(edgeHub.Object, this.entityStore);
            var sessionState = new SessionState(false);
            sessionState.RemoveSubscription(MethodPostTopicPrefix);
            Task setTask = sessionProvider.SetAsync(identity, sessionState);

            Assert.True(setTask.IsCompleted);
            edgeHub.Verify(x => x.RemoveSubscription("d1", DeviceSubscription.Methods), Times.Once);
        }

        [Fact]
        [Unit]
        public void TestGetAsync_NoSessionFound_ShouldReturnNull()
        {
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "deviceId");
            var sessionProvider = new SessionStateStoragePersistenceProvider(Mock.Of<IEdgeHub>(), this.entityStore);

            Task<ISessionState> task = sessionProvider.GetAsync(identity);

            Assert.Null(task.Result);
        }

        [Fact]
        [Unit]
        public void TestGetAsync_DeleteSession()
        {
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "deviceId");
            var sessionProvider = new SessionStateStoragePersistenceProvider(Mock.Of<IEdgeHub>(), this.entityStore);
            Task task = sessionProvider.DeleteAsync(identity, It.IsAny<ISessionState>());

            Assert.True(task.IsCompleted);
        }

        [Fact]
        [Unit]
        public async Task TestSetAsync_AddedMethodSubscription_ShouldSaveToStore()
        {
            var edgeHub = new Mock<IEdgeHub>();
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "d1");
            var sessionProvider = new SessionStateStoragePersistenceProvider(edgeHub.Object, this.entityStore);
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            await sessionProvider.SetAsync(identity, sessionState);

            ISessionState storedSession = await sessionProvider.GetAsync(identity);
            Assert.NotNull(storedSession);
            edgeHub.Verify(x => x.AddSubscription("d1", DeviceSubscription.Methods), Times.Once);

            // clean up
            await sessionProvider.DeleteAsync(identity, sessionState);
        }

        [Fact]
        [Unit]
        public async Task TestSetAsync_AddedMethodSubscription_TransientSession_ShouldNotSaveToStore()
        {
            var edgeHub = new Mock<IEdgeHub>();
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "d1");
            var sessionProvider = new SessionStateStoragePersistenceProvider(edgeHub.Object, this.entityStore);
            var sessionState = new SessionState(true);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            await sessionProvider.SetAsync(identity, sessionState);

            ISessionState storedSession = await sessionProvider.GetAsync(identity);
            Assert.Null(storedSession);
            edgeHub.Verify(x => x.AddSubscription("d1", DeviceSubscription.Methods), Times.Once);
        }

        [Fact]
        [Unit]
        public async Task TestSetAsync_RemovedSubscription_ShouldUpdateStore()
        {
            var edgeHub = new Mock<IEdgeHub>();
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "d1");
            var sessionProvider = new SessionStateStoragePersistenceProvider(edgeHub.Object, this.entityStore);
            var sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce);
            await sessionProvider.SetAsync(identity, sessionState);

            sessionState.RemoveSubscription(MethodPostTopicPrefix);
            await sessionProvider.SetAsync(identity, sessionState);

            ISessionState storedSession = await sessionProvider.GetAsync(identity);
            Assert.NotNull(storedSession);
            Assert.Null(storedSession.Subscriptions.SingleOrDefault(p => p.TopicFilter == MethodPostTopicPrefix));
            edgeHub.Verify(x => x.RemoveSubscription("d1", DeviceSubscription.Methods), Times.Once);

            // clean up
            await sessionProvider.DeleteAsync(identity, sessionState);
        }

        [Fact]
        [Unit]
        public async Task TestGetAsync_DeleteSession_DeleteFromStore()
        {
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "deviceId");

            var sessionProvider = new SessionStateStoragePersistenceProvider(Mock.Of<IEdgeHub>(), this.entityStore);
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
            var edgeHub = new Mock<IEdgeHub>();

            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "d1");
            var sessionProvider = new SessionStateStoragePersistenceProvider(edgeHub.Object, this.entityStore);
            ISessionState sessionState = await sessionProvider.GetAsync(identity);
            Assert.Null(sessionState);

            sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix, QualityOfService.AtLeastOnce);
            sessionState.AddOrUpdateSubscription(SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix, QualityOfService.AtMostOnce);
            sessionState.RemoveSubscription(SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix);
            await sessionProvider.SetAsync(identity, sessionState);

            edgeHub.Verify(x => x.AddSubscription("d1", DeviceSubscription.Methods), Times.Once);
            edgeHub.Verify(x => x.AddSubscription("d1", DeviceSubscription.C2D), Times.Once);
            edgeHub.Verify(x => x.RemoveSubscription("d1", DeviceSubscription.DesiredPropertyUpdates), Times.Once);

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

            edgeHub.Verify(x => x.AddSubscription("d1", DeviceSubscription.Methods), Times.Exactly(2));
            edgeHub.Verify(x => x.AddSubscription("d1", DeviceSubscription.C2D), Times.Exactly(2));
            edgeHub.Verify(x => x.RemoveSubscription("d1", DeviceSubscription.DesiredPropertyUpdates), Times.Exactly(2));

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

            edgeHub.Verify(x => x.AddSubscription("d1", DeviceSubscription.Methods), Times.Exactly(3));
            edgeHub.Verify(x => x.AddSubscription("d1", DeviceSubscription.C2D), Times.Exactly(3));
            edgeHub.Verify(x => x.RemoveSubscription("d1", DeviceSubscription.DesiredPropertyUpdates), Times.Exactly(3));
        }
    }
}
