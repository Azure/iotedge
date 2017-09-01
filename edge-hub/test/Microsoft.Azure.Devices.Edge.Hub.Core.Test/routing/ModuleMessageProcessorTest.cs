// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Moq;
    using Xunit;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using RoutingMessage = Microsoft.Azure.Devices.Routing.Core.Message;

    public class ModuleMessageProcessorTest
    {
        [Fact]
        [Unit]
        public void BasicTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var deviceProxyMock = new Mock<IDeviceProxy>();
            string modId = "device1/module1";
            string moduleEndpointId = "in1";

            var moduleEndpoint = new ModuleEndpoint(modId, moduleEndpointId, () => Option.Some(deviceProxyMock.Object), routingMessageConverter);
            IProcessor moduleMessageProcessor = moduleEndpoint.CreateProcessor();

            Assert.Equal(moduleEndpoint, moduleMessageProcessor.Endpoint);
            Assert.False(moduleMessageProcessor.ErrorDetectionStrategy.IsTransient(new Exception()));
        }

        [Fact]
        [Unit]
        public async Task ProcessAsyncTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();

            const string Mod1Id = "device1/module1";
            const string ModEndpointId = "in1";

            var deviceProxyMock = new Mock<IDeviceProxy>();
            deviceProxyMock.Setup(c => c.SendMessageAsync(It.IsAny<IMessage>(), It.Is<string>((ep) => ep.Equals(ModEndpointId, StringComparison.OrdinalIgnoreCase))))
                .Returns(Task.CompletedTask);

            deviceProxyMock.SetupGet(p => p.IsActive).Returns(true);

            byte[] messageBody = Encoding.UTF8.GetBytes("Message body");
            var properties = new Dictionary<string, string>()
            {
                { "Prop1", "Val1" },
                { "Prop2", "Val2" },
            };

            var systemProperties = new Dictionary<string, string>
            {
                { SystemProperties.DeviceId, Mod1Id }
            };

            var message1 = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, properties, systemProperties);
            var message2 = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, properties, systemProperties);

            var moduleEndpoint = new ModuleEndpoint(Mod1Id, ModEndpointId, () => Option.Some(deviceProxyMock.Object), routingMessageConverter);
            IProcessor moduleMessageProcessor = moduleEndpoint.CreateProcessor();

            ISinkResult<IRoutingMessage> result = await moduleMessageProcessor.ProcessAsync(message1, CancellationToken.None);
            Assert.NotNull(result);
            Assert.NotEmpty(result.Succeeded);
            Assert.Empty(result.Failed);
            Assert.Empty(result.InvalidDetailsList);
            Assert.False(result.SendFailureDetails.HasValue);

            ISinkResult<IRoutingMessage> resultBatch = await moduleMessageProcessor.ProcessAsync(new[] { message1, message2 }, CancellationToken.None);
            Assert.NotNull(resultBatch);
            Assert.NotEmpty(resultBatch.Succeeded);
            Assert.Empty(resultBatch.Failed);
            Assert.Empty(resultBatch.InvalidDetailsList);
            Assert.False(resultBatch.SendFailureDetails.HasValue);
        }
    }
}