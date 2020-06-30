// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Moq;

    public class MessageConfirmingTestBase
    {
        protected static IConnectionRegistry GetConnectionRegistry(Action<string, FeedbackStatus> confirmAction = null)
        {
            var connectionRegistry = Mock.Of<IConnectionRegistry>();

            Mock.Get(connectionRegistry)
                .Setup(cr => cr.GetUpstreamProxyAsync(It.IsAny<IIdentity>()))
                .Returns((IIdentity i) => CreateListenerFromIdentity(i));

            return connectionRegistry;

            Task<Option<IDeviceListener>> CreateListenerFromIdentity(IIdentity identity)
            {
                return Task.FromResult(Option.Some(new TestDeviceListener(identity, confirmAction) as IDeviceListener));
            }
        }

        protected static IMqttBridgeConnector GetConnector(SendCapture sendCapture = null)
        {
            var connector = Mock.Of<IMqttBridgeConnector>();
            Mock.Get(connector)
                .Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns((string topic, byte[] content) =>
                {
                    sendCapture?.Caputre(topic, content);
                    return Task.FromResult(true);
                });

            return connector;
        }

        protected class SendCapture
        {
            public string Topic { get; private set; }
            public byte[] Content { get; private set; }

            public void Caputre(string topic, byte[] content)
            {
                this.Topic = topic;
                this.Content = content;
            }
        }

        protected class TestDeviceListener : IDeviceListener
        {
            public TestDeviceListener(IIdentity identity, Action<string, FeedbackStatus> confirmAction = null)
            {
                this.Identity = identity;
                this.ConfirmAction = confirmAction;
            }

            public IIdentity Identity { get; }

            public Task AddDesiredPropertyUpdatesSubscription(string correlationId) => Task.CompletedTask;
            public Task AddSubscription(DeviceSubscription subscription) => Task.CompletedTask;
            public Task CloseAsync() => Task.CompletedTask;
            public Task ProcessDeviceMessageBatchAsync(IEnumerable<IMessage> message) => Task.CompletedTask;
            public Task ProcessMethodResponseAsync(IMessage message) => Task.CompletedTask;
            public Task RemoveDesiredPropertyUpdatesSubscription(string correlationId) => Task.CompletedTask;
            public Task RemoveSubscription(DeviceSubscription subscription) => Task.CompletedTask;
            public Task SendGetTwinRequest(string correlationId) => Task.CompletedTask;
            public Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage, string correlationId) => Task.CompletedTask;
            public Task ProcessDeviceMessageAsync(IMessage message) => Task.CompletedTask;

            public Task ProcessMessageFeedbackAsync(string messageId, FeedbackStatus feedbackStatus)
            {
                this.ConfirmAction?.Invoke(messageId, feedbackStatus);
                return Task.CompletedTask;
            }

            public void BindDeviceProxy(IDeviceProxy deviceProxy)
            {
            }

            Action<string, FeedbackStatus> ConfirmAction { get; }
        }
    }
}
