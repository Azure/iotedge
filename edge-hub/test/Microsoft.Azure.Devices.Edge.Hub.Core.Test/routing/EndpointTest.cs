// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Moq;
    using Xunit;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;

    public class EndpointTest
    {
        [Fact]
        [Unit]
        public void CloudEndpointTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();            
            var cloudProxyMock = new Mock<ICloudProxy>();
            string cloudEndpointId = Guid.NewGuid().ToString();

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, (id) => Option.Some(cloudProxyMock.Object), routingMessageConverter);

            Assert.Equal(cloudEndpointId, cloudEndpoint.Id);
            Assert.Equal("CloudEndpoint", cloudEndpoint.Type);
            Assert.Equal(cloudEndpointId, cloudEndpoint.Name);
            Assert.Equal(string.Empty, cloudEndpoint.IotHubName);

            IProcessor processor = cloudEndpoint.CreateProcessor();
            Assert.NotNull(processor);
            Assert.Equal(cloudEndpoint, processor.Endpoint);
        }

        [Fact]
        [Unit]
        public void ModuleEndpointTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var deviceProxyMock = new Mock<IDeviceProxy>();
            string moduleId = "device1/module1";
            string moduleEndpointAddress = "in1";

            var moduleEndpoint = new ModuleEndpoint(moduleId, moduleEndpointAddress, (id) => Option.Some(deviceProxyMock.Object), routingMessageConverter);

            Assert.Equal(moduleId, moduleEndpoint.Id);
            Assert.Equal("ModuleEndpoint", moduleEndpoint.Type);
            Assert.Equal(moduleId, moduleEndpoint.Name);
            Assert.Equal(string.Empty, moduleEndpoint.IotHubName);

            IProcessor processor = moduleEndpoint.CreateProcessor();
            Assert.NotNull(processor);
            Assert.Equal(moduleEndpoint, processor.Endpoint);
        }
    }
}