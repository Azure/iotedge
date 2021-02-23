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
                .Setup(cr => cr.GetOrCreateDeviceListenerAsync(It.IsAny<IIdentity>(), It.IsAny<bool>()))
                .Returns((IIdentity i, bool _) => CreateListenerFromIdentity(i));

            return connectionRegistry;

            Task<Option<IDeviceListener>> CreateListenerFromIdentity(IIdentity identity)
            {
                return Task.FromResult(Option.Some(new TestDeviceListener(identity, confirmAction) as IDeviceListener));
            }
        }

        protected static IMqttBrokerConnector GetConnector(SendCapture sendCapture = null)
        {
            var connector = Mock.Of<IMqttBrokerConnector>();
            Mock.Get(connector)
                .Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<bool>()))
                .Returns((string topic, byte[] content, bool retain) =>
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
            public Task RemoveSubscriptions() => Task.CompletedTask;
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

            public void BindDeviceProxy(IDeviceProxy deviceProxy, Action initWhenBound)
            {
            }

            Action<string, FeedbackStatus> ConfirmAction { get; }
        }
    }
}
