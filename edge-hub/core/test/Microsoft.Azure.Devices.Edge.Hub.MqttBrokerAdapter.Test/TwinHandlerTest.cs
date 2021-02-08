// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class TwinHandlerTest
    {
        [Theory]
        [MemberData(nameof(NonTwinTopics))]
        public async Task DoesNotHandleNonDirectMethodTopics(string topic)
        {
            var publishInfo = new MqttPublishInfo(topic, new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();

            var sut = new TwinHandler(connectionRegistry, identityProvider);

            var isHandled = await sut.HandleAsync(publishInfo);

            Assert.False(isHandled);
        }

        [Theory]
        [MemberData(nameof(TwinTopics))]
        public async Task HandlesTwinTopics(string topic)
        {
            var publishInfo = new MqttPublishInfo(topic, new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();

            var sut = new TwinHandler(connectionRegistry, identityProvider);

            var isHandled = await sut.HandleAsync(publishInfo);

            Assert.True(isHandled);
        }

        [Fact]
        public async Task CapturesDeviceIdentityFromTopic()
        {
            var milestone = new SemaphoreSlim(0, 1);
            var listenerCapture = new DeviceListenerCapture();
            var publishInfo = new MqttPublishInfo("$edgehub/captured_device_id/twin/get/?$rid=123", new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies(listenerCapture, milestone);

            var sut = new TwinHandler(connectionRegistry, identityProvider);

            _ = await sut.HandleAsync(publishInfo);
            await milestone.WaitAsync();

            Assert.IsType<DeviceIdentity>(listenerCapture.Captured.Identity);
            Assert.Equal("captured_device_id", ((DeviceIdentity)listenerCapture.Captured.Identity).DeviceId);
        }

        [Fact]
        public async Task CapturesModuleIdentityFromTopic()
        {
            var milestone = new SemaphoreSlim(0, 1);
            var listenerCapture = new DeviceListenerCapture();
            var publishInfo = new MqttPublishInfo("$edgehub/captured_device_id/captured_module_id/twin/get/?$rid=123", new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies(listenerCapture, milestone);

            var sut = new TwinHandler(connectionRegistry, identityProvider);

            _ = await sut.HandleAsync(publishInfo);
            await milestone.WaitAsync();

            Assert.IsType<ModuleIdentity>(listenerCapture.Captured.Identity);
            Assert.Equal("captured_device_id", ((ModuleIdentity)listenerCapture.Captured.Identity).DeviceId);
            Assert.Equal("captured_module_id", ((ModuleIdentity)listenerCapture.Captured.Identity).ModuleId);
        }

        [Fact]
        public async Task DoesNotHandleUnknownClient()
        {
            var publishInfo = new MqttPublishInfo("$edgehub/captured_device_id/captured_module_id/twin/get/?$rid=123", new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies(shouldFindProxy: false);

            var sut = new TelemetryHandler(connectionRegistry, identityProvider);

            var isHandled = await sut.HandleAsync(publishInfo);

            Assert.False(isHandled);
        }

        [Fact]
        public async Task CapturesTwinGetRidFromTopic()
        {
            var milestone = new SemaphoreSlim(0, 1);
            var listenerCapture = new DeviceListenerCapture();
            var publishInfo = new MqttPublishInfo("$edgehub/device_id/twin/get/?$rid=123", new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies(listenerCapture, milestone);

            var sut = new TwinHandler(connectionRegistry, identityProvider);

            _ = await sut.HandleAsync(publishInfo);
            await milestone.WaitAsync();

            Assert.Equal("123", listenerCapture.Captured.CapturedCorrelationId);
        }

        [Fact]
        public async Task CapturesTwinReportedRidFromTopic()
        {
            var milestone = new SemaphoreSlim(0, 1);
            var listenerCapture = new DeviceListenerCapture();
            var publishInfo = new MqttPublishInfo("$edgehub/device_id/twin/reported/?$rid=123", new byte[0]);
            var (connectionRegistry, identityProvider) = GetHandlerDependencies(listenerCapture, milestone);

            var sut = new TwinHandler(connectionRegistry, identityProvider);

            _ = await sut.HandleAsync(publishInfo);
            await milestone.WaitAsync();

            Assert.Equal("123", listenerCapture.Captured.CapturedCorrelationId);
        }

        [Fact]
        public async Task CapturesTwinReportedContentFromBody()
        {
            var milestone = new SemaphoreSlim(0, 1);
            var listenerCapture = new DeviceListenerCapture();
            var publishInfo = new MqttPublishInfo("$edgehub/device_id/twin/reported/?$rid=123", new byte[] { 1, 2, 3 });
            var (connectionRegistry, identityProvider) = GetHandlerDependencies(listenerCapture, milestone);

            var sut = new TwinHandler(connectionRegistry, identityProvider);

            _ = await sut.HandleAsync(publishInfo);
            await milestone.WaitAsync();

            Assert.Equal(new byte[] { 1, 2, 3 }, listenerCapture.Captured.CapturedMessage.Body);
        }

        [Fact]
        public async Task TwinUpdateRequiresMessageStatusCode()
        {
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();
            var connector = GetConnector();

            var identity = new DeviceIdentity("hub", "device_id");
            var twin = new EdgeMessage.Builder(new byte[] { 1, 2, 3 })
                                      .SetSystemProperties(new Dictionary<string, string>() { [SystemProperties.CorrelationId] = "123" })
                                      .Build();

            var sut = new TwinHandler(connectionRegistry, identityProvider);
            sut.SetConnector(connector);

            await sut.SendTwinUpdate(twin, identity, true);

            Mock.Get(connector)
                .Verify(c => c.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<bool>()), Times.Never());
        }

        [Fact]
        public async Task TwinUpdateRequiresMessageCorrelationId()
        {
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();
            var connector = GetConnector();

            var identity = new DeviceIdentity("hub", "device_id");
            var twin = new EdgeMessage.Builder(new byte[] { 1, 2, 3 })
                                      .SetSystemProperties(new Dictionary<string, string>() { [SystemProperties.StatusCode] = "200" })
                                      .Build();

            var sut = new TwinHandler(connectionRegistry, identityProvider);
            sut.SetConnector(connector);

            await sut.SendTwinUpdate(twin, identity, true);

            Mock.Get(connector)
                .Verify(c => c.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<bool>()), Times.Never());
        }

        [Fact]
        public async Task TwinUpdateEncodesPropertiesAndIdentityInTopic()
        {
            var sendCapture = new SendCapture();
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();
            var connector = GetConnector(sendCapture);

            var identity = new DeviceIdentity("hub", "device_id");
            var twin = new EdgeMessage.Builder(new byte[] { 1, 2, 3 })
                                      .SetSystemProperties(new Dictionary<string, string>()
                                      {
                                          [SystemProperties.StatusCode] = "200",
                                          [SystemProperties.CorrelationId] = "123"
                                      })
                                      .Build();

            var sut = new TwinHandler(connectionRegistry, identityProvider);
            sut.SetConnector(connector);

            await sut.SendTwinUpdate(twin, identity, true);

            Assert.Equal("$edgehub/device_id/twin/res/200/?$rid=123", sendCapture.Topic);
        }

        [Fact]
        public async Task TwinUpdateSendsMessageBody()
        {
            var sendCapture = new SendCapture();
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();
            var connector = GetConnector(sendCapture);

            var identity = new DeviceIdentity("hub", "device_id");
            var twin = new EdgeMessage.Builder(new byte[] { 1, 2, 3 })
                                      .SetSystemProperties(new Dictionary<string, string>()
                                      {
                                          [SystemProperties.StatusCode] = "200",
                                          [SystemProperties.CorrelationId] = "123"
                                      })
                                      .Build();

            var sut = new TwinHandler(connectionRegistry, identityProvider);
            sut.SetConnector(connector);

            await sut.SendTwinUpdate(twin, identity, true);

            Assert.Equal(new byte[] { 1, 2, 3 }, sendCapture.Content);
        }

        [Fact]
        public async Task DesiredUpdateRequiresVersion()
        {
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();
            var connector = GetConnector();

            var identity = new DeviceIdentity("hub", "device_id");
            var twin = new EdgeMessage.Builder(new byte[] { 1, 2, 3 }).Build();

            var sut = new TwinHandler(connectionRegistry, identityProvider);
            sut.SetConnector(connector);

            await sut.SendDesiredPropertiesUpdate(twin, identity, true);

            Mock.Get(connector)
                .Verify(c => c.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<bool>()), Times.Never());
        }

        [Fact]
        public async Task DesiredUpdateEncodesVersionAndIdentityInTopic()
        {
            var sendCapture = new SendCapture();
            var (connectionRegistry, identityProvider) = GetHandlerDependencies();
            var connector = GetConnector(sendCapture);

            var identity = new DeviceIdentity("hub", "device_id");
            var twin = new EdgeMessage.Builder(new byte[] { 1, 2, 3 })
                                      .SetSystemProperties(new Dictionary<string, string>()
                                      {
                                          [SystemProperties.Version] = "123"
                                      })
                                      .Build();

            var sut = new TwinHandler(connectionRegistry, identityProvider);
            sut.SetConnector(connector);

            await sut.SendDesiredPropertiesUpdate(twin, identity, true);

            Assert.Equal("$edgehub/device_id/twin/desired/?$version=123", sendCapture.Topic);
        }

        public static IEnumerable<object[]> TwinTopics()
        {
            var testStrings = new[] { "$edgehub/device_id/module_id/twin/get/?$rid=123",
                                      "$edgehub/device_id/twin/get/?$rid=123",
                                      "$edgehub/device_id/module_id/twin/reported/?$rid=123",
                                      "$edgehub/device_id/twin/reported/?$rid=123",
                                      "$iothub/device_id/module_id/twin/get/?$rid=123",
                                      "$iothub/device_id/twin/get/?$rid=123",
                                      "$iothub/device_id/module_id/twin/reported/?$rid=123",
                                      "$iothub/device_id/twin/reported/?$rid=123"
            };

            return testStrings.Select(s => new string[] { s });
        }

        public static IEnumerable<object[]> NonTwinTopics()
        {
            var testStrings = new[] { "$edgehub/device_id/module_id/twin/get/?$rid=",
                                      "$edgehub/device_id/module_id/twin/get",
                                      "$edgehub/device_id/module_id/twin/put/?$rid=123",
                                      "$edgehub/device_id/module_id/twin/reported/?$rid=",
                                      "$edgehub/twin/get/?$rid=123",
                                      "something/$edgehub/device_id/module_id/twin/get/?$rid=123",
                                      "$edgehub/device_id/modules/module_id/twin/get/?$rid=123"
            };

            return testStrings.Select(s => new string[] { s });
        }

        (IConnectionRegistry, IIdentityProvider) GetHandlerDependencies(DeviceListenerCapture listenerCapture = null, SemaphoreSlim milestone = null, bool shouldFindProxy = true)
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
                    listener = new TestDeviceListener(identity, milestone);
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

        class SendCapture
        {
            public string Topic { get; private set; }
            public byte[] Content { get; private set; }

            public void Caputre(string topic, byte[] content)
            {
                this.Topic = topic;
                this.Content = content;
            }
        }

        static IMqttBrokerConnector GetConnector(SendCapture sendCapture = null)
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

        class DeviceListenerCapture
        {
            public TestDeviceListener Captured { get; private set; }
            public void Capture(TestDeviceListener testListener) => this.Captured = testListener;
        }

        protected class TestDeviceListener : IDeviceListener
        {
            public TestDeviceListener(IIdentity identity, SemaphoreSlim milestone)
            {
                this.Identity = identity;
                this.Milestone = milestone;
            }

            public IIdentity Identity { get; }

            public Task AddDesiredPropertyUpdatesSubscription(string correlationId) => Task.CompletedTask;
            public Task AddSubscription(DeviceSubscription subscription) => Task.CompletedTask;
            public Task CloseAsync() => Task.CompletedTask;
            public Task ProcessDeviceMessageBatchAsync(IEnumerable<IMessage> message) => Task.CompletedTask;
            public Task RemoveDesiredPropertyUpdatesSubscription(string correlationId) => Task.CompletedTask;
            public Task RemoveSubscription(DeviceSubscription subscription) => Task.CompletedTask;
            public Task RemoveSubscriptions() => Task.CompletedTask;
            public Task ProcessDeviceMessageAsync(IMessage message) => Task.CompletedTask;
            public Task ProcessMessageFeedbackAsync(string messageId, FeedbackStatus feedbackStatus) => Task.CompletedTask;
            public Task ProcessMethodResponseAsync(IMessage message) => Task.CompletedTask;

            public Task SendGetTwinRequest(string correlationId)
            {
                this.CapturedCorrelationId = correlationId;
                this.Milestone?.Release();
                return Task.CompletedTask;
            }

            public Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage, string correlationId)
            {
                this.CapturedCorrelationId = correlationId;
                this.CapturedMessage = reportedPropertiesMessage;
                this.Milestone?.Release();
                return Task.CompletedTask;
            }

            public void BindDeviceProxy(IDeviceProxy deviceProxy)
            {
            }

            public void BindDeviceProxy(IDeviceProxy deviceProxy, Action initWhenBound)
            {
            }

            public string CapturedCorrelationId { get; private set; }
            public IMessage CapturedMessage { get; private set; }

            SemaphoreSlim Milestone { get; }
        }
    }
}
