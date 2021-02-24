// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DirectMethodHandlerTest
    {
        [Fact]
        public async Task EncodesModuleNameInTopic()
        {
            var capture = new SendCapture();
            var connector = GetConnector(capture);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();
            var identity = new ModuleIdentity("hub", "device_id", "module_id");
            var method = new DirectMethodRequest("12345", "method", new byte[] { 1, 2, 3 }, TimeSpan.FromSeconds(5));

            var sut = new DirectMethodHandler(connectionRegistry, identityProvider);
            sut.SetConnector(connector);

            await sut.CallDirectMethodAsync(method, identity, true);

            Assert.Equal("$edgehub/device_id/module_id/methods/post/method/?$rid=" + method.CorrelationId, capture.Topic);
        }

        [Fact]
        public async Task EncodesDeviceNameInTopic()
        {
            var capture = new SendCapture();
            var connector = GetConnector(capture);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();
            var identity = new DeviceIdentity("hub", "device_id");
            var method = new DirectMethodRequest("12345", "method", new byte[] { 1, 2, 3 }, TimeSpan.FromSeconds(5));

            var sut = new DirectMethodHandler(connectionRegistry, identityProvider);
            sut.SetConnector(connector);

            await sut.CallDirectMethodAsync(method, identity, true);

            Assert.Equal("$edgehub/device_id/methods/post/method/?$rid=" + method.CorrelationId, capture.Topic);
        }

        [Fact]
        public async Task SendsMessageDataAsPayload()
        {
            var capture = new SendCapture();
            var connector = GetConnector(capture);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();
            var identity = new DeviceIdentity("hub", "device_id");
            var method = new DirectMethodRequest("12345", "method", new byte[] { 1, 2, 3 }, TimeSpan.FromSeconds(5));

            var sut = new DirectMethodHandler(connectionRegistry, identityProvider);
            sut.SetConnector(connector);

            await sut.CallDirectMethodAsync(method, identity, true);

            Assert.Equal(new byte[] { 1, 2, 3 }, capture.Content);
        }

        [Theory]
        [MemberData(nameof(NonDirectMethodTopics))]
        public async Task DoesNotHandleNonDirectMethodTopics(string topic)
        {
            var publishInfo = new MqttPublishInfo(topic, new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();

            var sut = new DirectMethodHandler(connectionRegistry, identityProvider);

            var isHandled = await sut.HandleAsync(publishInfo);

            Assert.False(isHandled);
        }

        [Theory]
        [MemberData(nameof(DirectMethodTopics))]
        public async Task HandlesDirectMethodTopics(string topic)
        {
            var publishInfo = new MqttPublishInfo(topic, new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();

            var sut = new DirectMethodHandler(connectionRegistry, identityProvider);

            var isHandled = await sut.HandleAsync(publishInfo);

            Assert.True(isHandled);
        }

        [Fact]
        public async Task CapturesDeviceIdentityFromResponseTopic()
        {
            var listenerCapture = new DeviceListenerCapture();
            var publishInfo = new MqttPublishInfo("$edgehub/device_id/methods/res/200/?$rid=abcde", new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies(listenerCapture);

            var sut = new DirectMethodHandler(connectionRegistry, identityProvider);

            await sut.HandleAsync(publishInfo);

            Assert.IsType<DeviceIdentity>(listenerCapture.Captured.Identity);
            Assert.Equal("device_id", ((DeviceIdentity)listenerCapture.Captured.Identity).DeviceId);
        }

        [Fact]
        public async Task CapturesModuleIdentityFromResponseTopic()
        {
            var listenerCapture = new DeviceListenerCapture();
            var publishInfo = new MqttPublishInfo("$edgehub/device_id/module_id/methods/res/200/?$rid=abcde", new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies(listenerCapture);

            var sut = new DirectMethodHandler(connectionRegistry, identityProvider);

            await sut.HandleAsync(publishInfo);

            Assert.IsType<ModuleIdentity>(listenerCapture.Captured.Identity);
            Assert.Equal("device_id", ((ModuleIdentity)listenerCapture.Captured.Identity).DeviceId);
            Assert.Equal("module_id", ((ModuleIdentity)listenerCapture.Captured.Identity).ModuleId);
        }

        [Fact]
        public async Task AddsSystemPropertiesToMessage()
        {
            var listenerCapture = new DeviceListenerCapture();
            var publishInfo = new MqttPublishInfo("$edgehub/device_id/module_id/methods/res/200/?$rid=abcde", new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies(listenerCapture);

            var sut = new DirectMethodHandler(connectionRegistry, identityProvider);

            await sut.HandleAsync(publishInfo);

            var message = listenerCapture.Captured.CapturedMessage;

            Assert.Contains(SystemProperties.CorrelationId, message.Properties);
            Assert.Equal("abcde", message.Properties[SystemProperties.CorrelationId]);
            Assert.Contains(SystemProperties.StatusCode, message.Properties);
            Assert.Equal("200", message.Properties[SystemProperties.StatusCode]);
        }

        public static IEnumerable<object[]> NonDirectMethodTopics()
        {
            var testStrings = new[] { "something/$edgehub/device_id/methods/res/200/?$rid=123",
                                      "$edgehub/device_id/methods/res//?$rid=123",
                                      "$edgehub/device_id/methods/res/abc/?$rid=123",
                                      "$edgehub/device_id/methods/res/200/?$rid="
            };

            return testStrings.Select(s => new string[] { s });
        }

        public static IEnumerable<object[]> DirectMethodTopics()
        {
            var testStrings = new[] { "$edgehub/device_id/methods/res/200/?$rid=abcde",
                                      "$edgehub/device_id/module_id/methods/res/200/?$rid=abcde",
                                      "$iothub/device_id/methods/res/200/?$rid=abcde",
                                      "$iothub/device_id/module_id/methods/res/200/?$rid=abcde"
            };

            return testStrings.Select(s => new string[] { s });
        }

        (IConnectionRegistry, IIdentityProvider) GetHandlerDependencies(DeviceListenerCapture listenerCapture = null)
        {
            var connectionRegistry = Mock.Of<IConnectionRegistry>();
            var identityProvider = Mock.Of<IIdentityProvider>();

            Mock.Get(identityProvider)
                .Setup(ip => ip.Create(It.IsAny<string>()))
                .Returns((string id) => new DeviceIdentity("host", id));

            Mock.Get(identityProvider)
                .Setup(ip => ip.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string device_id, string module_id) => new ModuleIdentity("host", device_id, module_id));

            Mock.Get(connectionRegistry)
                .Setup(cr => cr.GetOrCreateDeviceListenerAsync(It.IsAny<IIdentity>(), It.IsAny<bool>()))
                .Returns((IIdentity i, bool _) => CreateListenerFromIdentity(i));

            return (connectionRegistry, identityProvider);

            Task<Option<IDeviceListener>> CreateListenerFromIdentity(IIdentity identity)
            {
                var listener = new TestDeviceListener(identity);
                if (listenerCapture != null)
                {
                    listenerCapture.Capture(listener);
                }

                return Task.FromResult(Option.Some(listener as IDeviceListener));
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

        class DeviceListenerCapture
        {
            public TestDeviceListener Captured { get; private set; }
            public void Capture(TestDeviceListener testListener) => this.Captured = testListener;
        }

        protected class TestDeviceListener : IDeviceListener
        {
            public TestDeviceListener(IIdentity identity)
            {
                this.Identity = identity;
            }

            public IIdentity Identity { get; }
            public IMessage CapturedMessage { get; private set; }

            public Task AddDesiredPropertyUpdatesSubscription(string correlationId) => Task.CompletedTask;
            public Task AddSubscription(DeviceSubscription subscription) => Task.CompletedTask;
            public Task CloseAsync() => Task.CompletedTask;
            public Task ProcessDeviceMessageBatchAsync(IEnumerable<IMessage> message) => Task.CompletedTask;
            public Task RemoveDesiredPropertyUpdatesSubscription(string correlationId) => Task.CompletedTask;
            public Task RemoveSubscription(DeviceSubscription subscription) => Task.CompletedTask;
            public Task RemoveSubscriptions() => Task.CompletedTask;
            public Task SendGetTwinRequest(string correlationId) => Task.CompletedTask;
            public Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage, string correlationId) => Task.CompletedTask;
            public Task ProcessDeviceMessageAsync(IMessage message) => Task.CompletedTask;
            public Task ProcessMessageFeedbackAsync(string messageId, FeedbackStatus feedbackStatus) => Task.CompletedTask;

            public Task ProcessMethodResponseAsync(IMessage message)
            {
                this.CapturedMessage = message;
                return Task.CompletedTask;
            }

            public void BindDeviceProxy(IDeviceProxy deviceProxy)
            {
            }

            public void BindDeviceProxy(IDeviceProxy deviceProxy, Action initWhenBound)
            {
            }
        }
    }
}
