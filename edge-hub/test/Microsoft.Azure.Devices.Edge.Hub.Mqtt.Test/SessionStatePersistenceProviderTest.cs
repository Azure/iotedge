// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DotNetty.Codecs.Mqtt.Packets;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    using Moq;

    using Xunit;

    using IProtocolgatewayDeviceIdentity = Microsoft.Azure.Devices.ProtocolGateway.Identity.IDeviceIdentity;

    public class SessionStatePersistenceProviderTest
    {
        public static IEnumerable<object[]> GetSubscriptionTopics()
        {
            var theoryData = new List<object[]>();

            theoryData.Add(new object[] { SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix, DeviceSubscription.DesiredPropertyUpdates });
            theoryData.Add(new object[] { $"{SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix}/somemorestuff", DeviceSubscription.DesiredPropertyUpdates });
            theoryData.Add(new object[] { $"SomeStartingStuff/{SessionStatePersistenceProvider.TwinSubscriptionTopicPrefix}", DeviceSubscription.Unknown });

            theoryData.Add(new object[] { SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix, DeviceSubscription.Methods });
            theoryData.Add(new object[] { $"{SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix}/somemorestuff", DeviceSubscription.Methods });
            theoryData.Add(new object[] { $"SomeStartingStuff/{SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix}", DeviceSubscription.Unknown });

            theoryData.Add(new object[] { SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix, DeviceSubscription.C2D });
            theoryData.Add(new object[] { $"{SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix}/somemorestuff", DeviceSubscription.Unknown });
            theoryData.Add(new object[] { $"devices/device1/{SessionStatePersistenceProvider.C2DSubscriptionTopicPrefix}", DeviceSubscription.C2D });

            theoryData.Add(new object[] { SessionStatePersistenceProvider.TwinResponseTopicFilter, DeviceSubscription.TwinResponse });
            theoryData.Add(new object[] { $"{SessionStatePersistenceProvider.TwinResponseTopicFilter}/somemorestuff", DeviceSubscription.Unknown });
            theoryData.Add(new object[] { $"devices/device1/{SessionStatePersistenceProvider.TwinResponseTopicFilter}", DeviceSubscription.Unknown });

            theoryData.Add(new object[] { "devices/device1/modules/module1/#", DeviceSubscription.ModuleMessages });
            theoryData.Add(new object[] { "devices/device1/modules/module1/", DeviceSubscription.Unknown });
            theoryData.Add(new object[] { "devices/device1/modules//#", DeviceSubscription.Unknown });
            theoryData.Add(new object[] { "devices/device1/modules/#", DeviceSubscription.Unknown });
            theoryData.Add(new object[] { "devices//modules/module1/#", DeviceSubscription.Unknown });
            theoryData.Add(new object[] { "devices/modules/module1/#", DeviceSubscription.Unknown });
            theoryData.Add(new object[] { "devices/device1/module1/#", DeviceSubscription.Unknown });
            theoryData.Add(new object[] { "devices/device1/modules/modules/#", DeviceSubscription.ModuleMessages });
            theoryData.Add(new object[] { "/devices/device1/modules/module1/#", DeviceSubscription.Unknown });
            theoryData.Add(new object[] { "devices/device1/modules/module1", DeviceSubscription.Unknown });

            return theoryData;
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetSubscriptionTopics))]
        public void GetSubscriptionTopicTest(string topicName, object subscriptionTopicObject)
        {
            var subscriptionTopic = (DeviceSubscription)subscriptionTopicObject;
            Assert.Equal(SessionStatePersistenceProvider.GetDeviceSubscription(topicName), subscriptionTopic);
        }

        [Fact]
        [Unit]
        public void TestCreate_ShouldReturn_Session()
        {
            var sessionProvider = new SessionStatePersistenceProvider(Mock.Of<IEdgeHub>());
            ISessionState session = sessionProvider.Create(true);

            Assert.NotNull(session);
            Assert.False(session.IsTransient);
        }

        [Fact]
        [Unit]
        public void TestGetAsync_DeleteSession()
        {
            var identity = new Mock<IProtocolgatewayDeviceIdentity>();
            identity.Setup(p => p.Id).Returns("device-id");

            var sessionProvider = new SessionStatePersistenceProvider(Mock.Of<IEdgeHub>());
            Task task = sessionProvider.DeleteAsync(identity.Object, It.IsAny<ISessionState>());

            Assert.True(task.IsCompleted);
        }

        [Fact]
        [Unit]
        public void TestGetAsync_NoSessionFound_ShouldReturnNull()
        {
            var identity = new Mock<IProtocolgatewayDeviceIdentity>();
            identity.Setup(p => p.Id).Returns("device-id");

            var sessionProvider = new SessionStatePersistenceProvider(Mock.Of<IEdgeHub>());

            Task<ISessionState> task = sessionProvider.GetAsync(identity.Object);

            Assert.Null(task.Result);
        }

        [Fact]
        [Unit]
        public void TestSetAsync_AddedMethodSubscription_ShouldComplete()
        {
            var edgeHub = new Mock<IEdgeHub>();
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "d1");
            var sessionProvider = new SessionStatePersistenceProvider(edgeHub.Object);
            ISessionState sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix, QualityOfService.AtLeastOnce);
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
            var sessionProvider = new SessionStatePersistenceProvider(edgeHub.Object);
            ISessionState sessionState = new SessionState(false);
            sessionState.RemoveSubscription(SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix);
            Task setTask = sessionProvider.SetAsync(identity, sessionState);

            Assert.True(setTask.IsCompleted);
            edgeHub.Verify(x => x.RemoveSubscription("d1", DeviceSubscription.Methods), Times.Once);
        }

        [Fact]
        [Unit]
        public void TestSetAsync_SetMultipleSubscriptions_ShouldComplete()
        {
            var edgeHub = new Mock<IEdgeHub>();
            var identity = Mock.Of<IProtocolgatewayDeviceIdentity>(i => i.Id == "d1");
            var sessionProvider = new SessionStatePersistenceProvider(edgeHub.Object);

            ISessionState sessionState = new SessionState(false);
            sessionState.AddOrUpdateSubscription(SessionStatePersistenceProvider.MethodSubscriptionTopicPrefix, QualityOfService.AtLeastOnce);
            sessionState.AddOrUpdateSubscription(SessionStatePersistenceProvider.TwinResponseTopicFilter, QualityOfService.AtLeastOnce);
            Task setTask = sessionProvider.SetAsync(identity, sessionState);

            Assert.True(setTask.IsCompleted);
            edgeHub.Verify(x => x.AddSubscription("d1", DeviceSubscription.Methods), Times.Once);
        }
    }
}
