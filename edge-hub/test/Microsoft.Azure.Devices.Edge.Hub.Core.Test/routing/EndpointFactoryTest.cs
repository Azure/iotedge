// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Moq;
    using Xunit;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;

    [Unit]
    public class EndpointFactoryTest
    {
        readonly EndpointFactory endpointFactory;

        public EndpointFactoryTest()
        {
            var connectionManager = new Mock<IConnectionManager>();
            var messageConverter = new Mock<Core.IMessageConverter<IRoutingMessage>>();
            this.endpointFactory = new EndpointFactory(connectionManager.Object, messageConverter.Object, "Device1");
        }

        [Fact]
        public void TestCreateSystemEndpoint()
        {
            Endpoint endpoint = this.endpointFactory.CreateSystemEndpoint("$upstream");
            Assert.NotNull(endpoint);
            Assert.IsType<CloudEndpoint>(endpoint);
        }

        [Fact]
        public void TestCreateFunctionEndpoint()
        {
            Endpoint endpoint = this.endpointFactory.CreateFunctionEndpoint("BrokeredEndpoint", "/modules/alertLogic/inputs/in1");
            Assert.NotNull(endpoint);
            
            var moduleEndpoint = endpoint as ModuleEndpoint;
            Assert.NotNull(moduleEndpoint);
            Assert.Equal("Device1/alertLogic", moduleEndpoint.Id);
            Assert.Equal("in1", moduleEndpoint.EndpointAddress);
        }

        [Theory]
        [InlineData("upstream")]
        [InlineData("FooBar")]
        [InlineData("")]
        [InlineData(null)]
        public void TestCreateSystemEndpointInvalidCases(string systemEndpoint)
        {
            Assert.Throws<InvalidOperationException>(() =>this.endpointFactory.CreateSystemEndpoint(systemEndpoint));
        }

        [Theory]
        [InlineData("NonBrokeredEndpoint", "/modules/mod1/inputs/in1")]
        [InlineData("", "/modules/mod1/inputs/in1")]
        [InlineData(null, "/modules/mod1/inputs/in1")]
        [InlineData("BrokeredEndpoint", "/modules/mod1")]
        [InlineData("BrokeredEndpoint", "mod1, in1")]
        [InlineData("BrokeredEndpoint", "/modules///alertLogic/inputs/in1")]
        [InlineData("BrokeredEndpoint", "//modules/alertLogic/inputs/in1")]
        [InlineData("BrokeredEndpoint", "/modules/alertLogic/inputs/in1//")]
        public void TestCreateFunctionEndpointInvalidCases(string function, string endpointAddress)
        {
            Assert.Throws<InvalidOperationException>(() => this.endpointFactory.CreateFunctionEndpoint(function, endpointAddress));
        }
    }
}