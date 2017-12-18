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
            sessionState.AddOrUpdateSubscription(SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix, QualityOfService.AtLeastOnce);
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
            var sessionProvider = new SessionStatePersistenceProvider(connectionManager.Object);
            ISessionState sessionState = new SessionState(false);
            sessionState.RemoveSubscription(SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix);
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

        [Fact]
        [Unit]
        public void TestSetAsync_SetMultipleSubscriptions_ShouldComplete()
        {
            var cloudProxy = new Mock<ICloudProxy>();
            Option<ICloudProxy> cloudProxyOption = Option.Some(cloudProxy.Object);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(cm => cm.GetCloudConnection(It.IsAny<string>())).Returns(cloudProxyOption);

            IProtocolgatewayDeviceIdentity identity = new Mock<IProtocolgatewayDeviceIdentity>().Object;
            var sessionProvider = new SessionStatePersistenceProvider(connectionManager.Object);
            ISessionState sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix, QualityOfService.AtLeastOnce);
            sessionState.AddOrUpdateSubscription(SessionStatePersistenceProvider.TwinResponseTopicFilter, QualityOfService.AtLeastOnce);
            Task setTask = sessionProvider.SetAsync(identity, sessionState);

            Assert.True(setTask.IsCompleted);
            cloudProxy.Verify(x => x.SetupCallMethodAsync(), Times.Once);
        }

        public static IEnumerable<object[]> GetSubscriptionTopics()
        {
            var theoryData = new List<object[]>();

            theoryData.Add(new object[] { SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix, SessionStatePersistenceProvider.SubscriptionTopic.TwinDesiredProperties });
            theoryData.Add(new object[] { $"{SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix}/somemorestuff", SessionStatePersistenceProvider.SubscriptionTopic.TwinDesiredProperties });
            theoryData.Add(new object[] { $"SomeStartingStuff/{SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix}", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });

            theoryData.Add(new object[] { SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix, SessionStatePersistenceProvider.SubscriptionTopic.Method });
            theoryData.Add(new object[] { $"{SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix}/somemorestuff", SessionStatePersistenceProvider.SubscriptionTopic.Method });
            theoryData.Add(new object[] { $"SomeStartingStuff/{SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix}", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });

            theoryData.Add(new object[] { SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix, SessionStatePersistenceProvider.SubscriptionTopic.C2D });
            theoryData.Add(new object[] { $"{SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix}/somemorestuff", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });
            theoryData.Add(new object[] { $"devices/device1/{SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix}", SessionStatePersistenceProvider.SubscriptionTopic.C2D });

            theoryData.Add(new object[] { SessionStatePersistenceProvider.TwinResponseTopicFilter, SessionStatePersistenceProvider.SubscriptionTopic.TwinResponse });
            theoryData.Add(new object[] { $"{SessionStatePersistenceProvider.TwinResponseTopicFilter}/somemorestuff", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });
            theoryData.Add(new object[] { $"devices/device1/{SessionStatePersistenceProvider.TwinResponseTopicFilter}", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });

            theoryData.Add(new object[] { "devices/device1/modules/module1/#", SessionStatePersistenceProvider.SubscriptionTopic.ModuleMessage });
            theoryData.Add(new object[] { "devices/device1/modules/module1/", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });
            theoryData.Add(new object[] { "devices/device1/modules//#", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });
            theoryData.Add(new object[] { "devices/device1/modules/#", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });
            theoryData.Add(new object[] { "devices//modules/module1/#", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });
            theoryData.Add(new object[] { "devices/modules/module1/#", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });
            theoryData.Add(new object[] { "devices/device1/module1/#", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });
            theoryData.Add(new object[] { "devices/device1/modules/modules/#", SessionStatePersistenceProvider.SubscriptionTopic.ModuleMessage });
            theoryData.Add(new object[] { "/devices/device1/modules/module1/#", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });
            theoryData.Add(new object[] { "devices/device1/modules/module1", SessionStatePersistenceProvider.SubscriptionTopic.Unknown });

            return theoryData;
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetSubscriptionTopics))]
        public void GetSubscriptionTopicTest(string topicName, object subscriptionTopicObject)
        {
            var subscriptionTopic = (SessionStatePersistenceProvider.SubscriptionTopic)subscriptionTopicObject;
            Assert.Equal(SessionStatePersistenceProvider.GetSubscriptionTopic(topicName), subscriptionTopic);
        }
    }
}
