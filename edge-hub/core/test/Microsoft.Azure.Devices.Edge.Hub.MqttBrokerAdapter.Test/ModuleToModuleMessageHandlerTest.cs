// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ModuleToModuleMessageHandlerTest : MessageConfirmingTestBase
    {
        [Fact]
        public async Task EncodesModuleNameInTopic()
        {
            var capture = new SendCapture();
            var connector = GetConnector(capture);
            var connectionRegistry = GetConnectionRegistry();
            var identityProvider = new IdentityProvider("hub");
            var identity = new ModuleIdentity("hub", "device_id", "module_id");
            var message = new EdgeMessage
                                .Builder(new byte[] { 0x01, 0x02, 0x03 })
                                .SetSystemProperties(new Dictionary<string, string>() { [SystemProperties.LockToken] = "12345" })
                                .Build();

            using var sut = new ModuleToModuleMessageHandler(connectionRegistry, identityProvider, GetAckTimeout());
            sut.SetConnector(connector);

            await sut.SendModuleToModuleMessageAsync(message, "some_input", identity, true);

            Assert.Equal("$edgehub/device_id/module_id/12345/inputs/some_input/", capture.Topic);
        }

        [Fact]
        public async Task EncodesPropertiesInTopic()
        {
            var capture = new SendCapture();
            var connector = GetConnector(capture);
            var connectionRegistry = GetConnectionRegistry();
            var identityProvider = new IdentityProvider("hub");
            var identity = new ModuleIdentity("hub", "device_id", "module_id");
            var message = new EdgeMessage
                                .Builder(new byte[] { 0x01, 0x02, 0x03 })
                                .SetProperties(new Dictionary<string, string>() { ["prop1"] = "value1", ["prop2"] = "value2" })
                                .SetSystemProperties(new Dictionary<string, string>() { ["userId"] = "userid", ["cid"] = "corrid", [SystemProperties.LockToken] = "12345" })
                                .Build();

            using var sut = new ModuleToModuleMessageHandler(connectionRegistry, identityProvider, GetAckTimeout());
            sut.SetConnector(connector);

            await sut.SendModuleToModuleMessageAsync(message, "some_input", identity, true);

            Assert.StartsWith("$edgehub/device_id/module_id/12345/inputs/some_input/", capture.Topic);
            Assert.Contains("prop1=value1", capture.Topic);
            Assert.Contains("prop2=value2", capture.Topic);
            Assert.Contains("%24.uid=userid", capture.Topic);
            Assert.Contains("%24.cid=corrid", capture.Topic);
        }

        [Fact]
        public async Task SendsMessageDataAsPayload()
        {
            var capture = new SendCapture();
            var connector = GetConnector(capture);
            var connectionRegistry = GetConnectionRegistry();
            var identityProvider = new IdentityProvider("hub");
            var identity = new ModuleIdentity("hub", "device_id", "module_id");
            var message = new EdgeMessage
                                .Builder(new byte[] { 0x01, 0x02, 0x03 })
                                .SetSystemProperties(new Dictionary<string, string>() { [SystemProperties.LockToken] = "12345" })
                                .Build();

            using var sut = new ModuleToModuleMessageHandler(connectionRegistry, identityProvider, GetAckTimeout());
            sut.SetConnector(connector);

            await sut.SendModuleToModuleMessageAsync(message, "some_input", identity, true);

            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, capture.Content);
        }

        [Fact]
        public async Task ConfirmsMessageAfterSent()
        {
            var capturedLockId = default(string);
            var capturedStatus = (FeedbackStatus)(-1);

            var connectionRegistry = GetConnectionRegistry(
                                        confirmAction: (lockId, status) =>
                                        {
                                            capturedLockId = lockId;
                                            capturedStatus = status;
                                        });

            var identityProvider = new IdentityProvider("hub");
            var connector = GetConnector();

            var identity = new ModuleIdentity("hub", "device_id", "module_id");
            var message = new EdgeMessage
                                .Builder(new byte[] { 0x01, 0x02, 0x03 })
                                .SetSystemProperties(new Dictionary<string, string>() { [SystemProperties.LockToken] = "12345" })
                                .Build();

            using var sut = new ModuleToModuleMessageHandler(connectionRegistry, identityProvider, GetAckTimeout());
            sut.SetConnector(connector);

            await sut.SendModuleToModuleMessageAsync(message, "some_input", identity, true);
            await sut.HandleAsync(new MqttPublishInfo("$edgehub/delivered", Encoding.UTF8.GetBytes(@"""$edgehub/device_id/module_id/12345/inputs/""")));

            Assert.Equal("12345", capturedLockId);
            Assert.Equal(FeedbackStatus.Complete, capturedStatus);
        }

        [Fact]
        public async Task DoesNotSendToDevice()
        {
            var connector = GetConnector();
            var connectionRegistry = GetConnectionRegistry();
            var identity = new DeviceIdentity("hub", "device_id");
            var message = new EdgeMessage
                                .Builder(new byte[] { 0x01, 0x02, 0x03 })
                                .SetSystemProperties(new Dictionary<string, string>() { [SystemProperties.LockToken] = "12345" })
                                .Build();

            var identityProvider = new IdentityProvider("hub");

            using var sut = new ModuleToModuleMessageHandler(connectionRegistry, identityProvider, GetAckTimeout());
            sut.SetConnector(connector);

            await sut.SendModuleToModuleMessageAsync(message, "some_input", identity, true);

            Mock.Get(connector)
                .Verify(c => c.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<bool>()), Times.Never());
        }

        ModuleToModuleResponseTimeout GetAckTimeout() => new ModuleToModuleResponseTimeout(TimeSpan.FromSeconds(30));
    }
}
