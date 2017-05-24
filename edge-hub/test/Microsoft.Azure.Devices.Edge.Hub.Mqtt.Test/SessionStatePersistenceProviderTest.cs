// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Mqtt.Packets;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Moq;
    using Xunit;
    using IProtocolgatewayDeviceIdentity = Microsoft.Azure.Devices.ProtocolGateway.Identity.IDeviceIdentity;

    public class SessionStatePersistenceProviderTest
    {
        const string MethodPostTopicPrefix = "$iothub/methods/POST/";

        [Fact]
        [Unit]
        public void TestCreate_ShouldReturn_Session()
        {
            IConnectionManager connectionManager = new Mock<IConnectionManager>().Object;
            var sessionProvider = new SessionStatePersistenceProvider(connectionManager);
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
            var sessionProvider = new SessionStatePersistenceProvider(connectionManager.Object);
            ISessionState sessionState = new SessionState(false);
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

            var currentSubs = new List<ISubscription>()
            {
                new TransientSubscription(MethodPostTopicPrefix, QualityOfService.AtLeastOnce)
            };
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(cm => cm.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxyOption);

            IProtocolgatewayDeviceIdentity identity = new Mock<IProtocolgatewayDeviceIdentity>().Object;
            var sessionProvider = new SessionStatePersistenceProvider(connectionManager.Object);
            ISessionState sessionState = new SessionState(false);
            sessionState.RemoveSubscription(MethodPostTopicPrefix);
            Task setTask = sessionProvider.SetAsync(identity, sessionState);

            Assert.True(setTask.IsCompleted);
            cloudProxy.Verify(x => x.RemoveCallMethodAsync(), Times.Once);
        }

        [Fact]
        [Unit]
        public void TestGetAsync_NoSessionFound_ShouldReturnNull()
        {
            var currentSubs = new List<ISubscription>();
            
            var connectionManager = new Mock<IConnectionManager>();
            var identity = new Mock<IProtocolgatewayDeviceIdentity>();
            identity.Setup(p => p.Id).Returns("device-id");

            var sessionProvider = new SessionStatePersistenceProvider(connectionManager.Object);

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

            var sessionProvider = new SessionStatePersistenceProvider(connectionManager.Object);
            Task task = sessionProvider.DeleteAsync(identity.Object, It.IsAny<ISessionState>());

            Assert.True(task.IsCompleted);
        }
    }
}
