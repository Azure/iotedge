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
    public class TelemetryHandlerTest
    {
        [Theory]
        [MemberData(nameof(NonTelemetryTopics))]
        public async Task DoesNotHandleNonTelemetryTopics(string topic)
        {
            var publishInfo = new MqttPublishInfo(topic, new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();

            var sut = new TelemetryHandler(connectionRegistry, identityProvider);

            var isHandled = await sut.HandleAsync(publishInfo);

            Assert.False(isHandled);
        }

        [Theory]
        [MemberData(nameof(TelemetryTopics))]
        public async Task HandlesTelemetryTopics(string topic)
        {
            var publishInfo = new MqttPublishInfo(topic, new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();

            var sut = new TelemetryHandler(connectionRegistry, identityProvider);

            var isHandled = await sut.HandleAsync(publishInfo);

            Assert.True(isHandled);
        }

        [Fact]
        public async Task CapturesDeviceIdentityFromTopic()
        {
            var publishInfo = new MqttPublishInfo("$edgehub/captured_device_id/messages/events/prop1=val1&prop2=val2", new byte[0]);
            var (connectionRegistry, _) = GetHandlerDependencies();
            var identityProvider = Mock.Of<IIdentityProvider>();

            string passedDeviceId = null;

            Mock.Get(identityProvider)
                .Setup(ip => ip.Create(It.IsAny<string>()))
                .Returns((string device_id) =>
                {
                    passedDeviceId = device_id;
                    return new DeviceIdentity("host", device_id);
                });

            var sut = new TelemetryHandler(connectionRegistry, identityProvider);

            _ = await sut.HandleAsync(publishInfo);

            Assert.Equal("captured_device_id", passedDeviceId);
        }

        [Fact]
        public async Task CapturesModuleIdentityFromTopic()
        {
            var publishInfo = new MqttPublishInfo("$edgehub/captured_device_id/captured_module_id/messages/events/prop1=val1&prop2=val2", new byte[0]);
            var (connectionRegistry, _) = GetHandlerDependencies();
            var identityProvider = Mock.Of<IIdentityProvider>();

            string passedDeviceId = null, passedModuleId = null;

            Mock.Get(identityProvider)
                .Setup(ip => ip.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string device_id, string module_id) =>
                    {
                        passedDeviceId = device_id;
                        passedModuleId = module_id;
                        return new ModuleIdentity("host", device_id, module_id);
                    });

            var sut = new TelemetryHandler(connectionRegistry, identityProvider);

            _ = await sut.HandleAsync(publishInfo);

            Assert.Equal("captured_device_id", passedDeviceId);
            Assert.Equal("captured_module_id", passedModuleId);
        }

        [Fact]
        public async Task DoesNotHandleUnknownClient()
        {
            var publishInfo = new MqttPublishInfo("$edgehub/device_id/messages/events/prop1=val1&prop2=val2", new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies(shouldFindProxy: false);

            var sut = new TelemetryHandler(connectionRegistry, identityProvider);

            var isHandled = await sut.HandleAsync(publishInfo);

            Assert.False(isHandled);
        }

        [Fact]
        public async Task InternalMessageCapturesBody()
        {
            var deviceListenerCapture = new DeviceListenerCapture();

            var publishInfo = new MqttPublishInfo("$edgehub/device_id/messages/events/prop1=val1&prop2=val2", new byte[] { 0x01, 0x02, 0x03 });
            var (connectionRegistry, identityProvider) = GetHandlerDependencies(listenerCapture: deviceListenerCapture);

            var sut = new TelemetryHandler(connectionRegistry, identityProvider);

            var isHandled = await sut.HandleAsync(publishInfo);

            Assert.True(isHandled);
            Assert.NotNull(deviceListenerCapture.Captured);
            Assert.NotNull(deviceListenerCapture.Captured.LastCapturedMessage);

            var capturedMessage = deviceListenerCapture.Captured.LastCapturedMessage;

            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, capturedMessage.Body);
        }

        [Fact]
        public async Task InternalMessageCapturesProperties()
        {
            var deviceListenerCapture = new DeviceListenerCapture();

            var publishInfo = new MqttPublishInfo("$edgehub/device_id/messages/events/prop1=val1&prop2=val2", new byte[] { 0x01, 0x02, 0x03 });
            var (connectionRegistry, identityProvider) = GetHandlerDependencies(listenerCapture: deviceListenerCapture);

            var sut = new TelemetryHandler(connectionRegistry, identityProvider);

            var isHandled = await sut.HandleAsync(publishInfo);

            Assert.True(isHandled);
            Assert.NotNull(deviceListenerCapture.Captured);
            Assert.NotNull(deviceListenerCapture.Captured.LastCapturedMessage);

            var capturedMessage = deviceListenerCapture.Captured.LastCapturedMessage;
            var expectedProperties = new Dictionary<string, string>()
            {
                ["prop1"] = "val1",
                ["prop2"] = "val2",
            };

            Assert.Equal(expectedProperties, capturedMessage.Properties);
        }

        [Fact]
        public async Task InternalMessageCapturesSystemProperties()
        {
            var deviceListenerCapture = new DeviceListenerCapture();

            var publishInfo = new MqttPublishInfo("$edgehub/device_id/messages/events/%24.uid=userid&%24.cid=corrid", new byte[] { 0x01, 0x02, 0x03 });
            var (connectionRegistry, _) = GetHandlerDependencies(listenerCapture: deviceListenerCapture);
            var identityProvider = Mock.Of<IIdentityProvider>();

            string passedDeviceId = null;

            Mock.Get(identityProvider)
                .Setup(ip => ip.Create(It.IsAny<string>()))
                .Returns((string device_id) =>
                {
                    passedDeviceId = device_id;
                    return new DeviceIdentity("host", device_id);
                });

            var sut = new TelemetryHandler(connectionRegistry, identityProvider);

            var isHandled = await sut.HandleAsync(publishInfo);

            Assert.True(isHandled);
            Assert.NotNull(deviceListenerCapture.Captured);
            Assert.NotNull(deviceListenerCapture.Captured.LastCapturedMessage);

            var capturedMessage = deviceListenerCapture.Captured.LastCapturedMessage;
            var expectedProperties = new Dictionary<string, string>()
            {
                ["userId"] = "userid",
                ["cid"] = "corrid",
                ["connectionDeviceId"] = passedDeviceId
            };

            Assert.Equal(expectedProperties, capturedMessage.SystemProperties);
        }

        (IConnectionRegistry, IIdentityProvider) GetHandlerDependencies(bool shouldFindProxy = true, DeviceListenerCapture listenerCapture = null)
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
                var listener = default(TestDeviceListener);

                if (shouldFindProxy)
                {
                    listener = new TestDeviceListener(identity);
                    if (listenerCapture != null)
                    {
                        listenerCapture.Capture(listener);
                    }
                }
                
                return Task.FromResult(listener != null
                                            ? Option.Some(listener as IDeviceListener)
                                            : Option.None<IDeviceListener>());
            }
        }

        public static IEnumerable<object[]> NonTelemetryTopics()
        {
            var testStrings = new[] { "$edgehub/device_id",
                                      "$edgehub/device_id/messages",
                                      "$edgehub/device_id/modules/messages",
                                      "$edgehub/device_id/modules/module_id",
                                      "something/$edgehub/device_id/messages/events",
                                      "$edgehub/device_id/methods/res",
            };

            return testStrings.Select(s => new string[] { s });
        }

        const string TelemetryDevice = "$edgehub/+/messages/events/#";
        const string TelemetryModule = "$edgehub/+/modules/+/messages/events/#";

        public static IEnumerable<object[]> TelemetryTopics()
        {
            var testStrings = new[] { "$edgehub/device_id/messages/events",
                                      "$edgehub/device_id/messages/events/",
                                      "$edgehub/device_id/messages/events/prop1=val1&prop2=val2",
                                      "$edgehub/device_id/module_id/messages/events",
                                      "$edgehub/device_id/module_id/messages/events/",
                                      "$edgehub/device_id/module_id/messages/events/prop1=val1&prop2=val2",
                                      "$iothub/device_id/messages/events",
                                      "$iothub/device_id/messages/events/",
                                      "$iothub/device_id/messages/events/prop1=val1&prop2=val2",
                                      "$iothub/device_id/module_id/messages/events",
                                      "$iothub/device_id/module_id/messages/events/",
                                      "$iothub/device_id/module_id/messages/events/prop1=val1&prop2=val2"
            };

            return testStrings.Select(s => new string[] { s });
        }

        class DeviceListenerCapture
        {
            public TestDeviceListener Captured { get; private set; }
            public void Capture(TestDeviceListener testListener) => this.Captured = testListener;
        }

        class TestDeviceListener : IDeviceListener
        { 
            public TestDeviceListener(IIdentity identity)
            {
                this.Identity = identity;
            }

            public IMessage LastCapturedMessage { get; set; }

            public IIdentity Identity { get; }

            public Task AddDesiredPropertyUpdatesSubscription(string correlationId) => Task.CompletedTask;
            public Task AddSubscription(DeviceSubscription subscription) => Task.CompletedTask;            
            public Task CloseAsync() => Task.CompletedTask;
            public Task ProcessDeviceMessageBatchAsync(IEnumerable<IMessage> message) => Task.CompletedTask;
            public Task ProcessMessageFeedbackAsync(string messageId, FeedbackStatus feedbackStatus) => Task.CompletedTask;
            public Task ProcessMethodResponseAsync(IMessage message) => Task.CompletedTask;
            public Task RemoveDesiredPropertyUpdatesSubscription(string correlationId) => Task.CompletedTask;
            public Task RemoveSubscription(DeviceSubscription subscription) => Task.CompletedTask;
            public Task RemoveSubscriptions() => Task.CompletedTask;
            public Task SendGetTwinRequest(string correlationId) => Task.CompletedTask;
            public Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage, string correlationId) => Task.CompletedTask;

            public Task ProcessDeviceMessageAsync(IMessage message)
            {
                this.LastCapturedMessage = message;
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
