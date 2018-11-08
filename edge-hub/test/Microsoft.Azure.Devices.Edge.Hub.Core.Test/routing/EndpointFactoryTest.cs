// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
        public void TestCreateDuplicateFunctionsEndpoint()
        {
            Endpoint endpoint1 = this.endpointFactory.CreateFunctionEndpoint("BrokeredEndpoint", "/modules/alertLogic/inputs/in1");
            Assert.NotNull(endpoint1);

            var moduleEndpoint1 = endpoint1 as ModuleEndpoint;
            Assert.NotNull(moduleEndpoint1);
            Assert.Equal("Device1/alertLogic/in1", moduleEndpoint1.Id);
            Assert.Equal("in1", moduleEndpoint1.Input);

            Endpoint endpoint2 = this.endpointFactory.CreateFunctionEndpoint("BrokeredEndpoint", "/modules/alertLogic/inputs/in1");
            Assert.NotNull(endpoint2);

            var moduleEndpoint2 = endpoint2 as ModuleEndpoint;
            Assert.NotNull(moduleEndpoint2);
            Assert.Equal("Device1/alertLogic/in1", moduleEndpoint2.Id);
            Assert.Equal("in1", moduleEndpoint2.Input);

            Endpoint endpoint3 = this.endpointFactory.CreateFunctionEndpoint("BrokeredEndpoint", "/modules/alertLogic/inputs/in2");
            Assert.NotNull(endpoint3);

            var moduleEndpoint3 = endpoint3 as ModuleEndpoint;
            Assert.NotNull(moduleEndpoint3);
            Assert.Equal("Device1/alertLogic/in2", moduleEndpoint3.Id);
            Assert.Equal("in2", moduleEndpoint3.Input);

            Endpoint endpoint4 = this.endpointFactory.CreateFunctionEndpoint("BrokeredEndpoint", "/modules/alertLogic2/inputs/in1");
            Assert.NotNull(endpoint4);

            var moduleEndpoint4 = endpoint4 as ModuleEndpoint;
            Assert.NotNull(moduleEndpoint4);
            Assert.Equal("Device1/alertLogic2/in1", moduleEndpoint4.Id);
            Assert.Equal("in1", moduleEndpoint4.Input);

            Assert.Equal(endpoint1, endpoint2);
            Assert.NotEqual(endpoint1, endpoint3);
            Assert.NotEqual(endpoint1, endpoint4);
            Assert.NotEqual(endpoint3, endpoint4);
        }

        [Fact]
        public void TestCreateFunctionEndpoint()
        {
            Endpoint endpoint = this.endpointFactory.CreateFunctionEndpoint("BrokeredEndpoint", "/modules/alertLogic/inputs/in1");
            Assert.NotNull(endpoint);

            var moduleEndpoint = endpoint as ModuleEndpoint;
            Assert.NotNull(moduleEndpoint);
            Assert.Equal("Device1/alertLogic/in1", moduleEndpoint.Id);
            Assert.Equal("in1", moduleEndpoint.Input);
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

        [Fact]
        public void TestCreateSystemEndpoint()
        {
            Endpoint endpoint = this.endpointFactory.CreateSystemEndpoint("$upstream");
            Assert.NotNull(endpoint);
            Assert.IsType<CloudEndpoint>(endpoint);

            Endpoint endpoint2 = this.endpointFactory.CreateSystemEndpoint("$upstream");
            Assert.NotNull(endpoint2);
            Assert.IsType<CloudEndpoint>(endpoint2);
            Assert.Equal(endpoint, endpoint2);
        }

        [Theory]
        [InlineData("upstream")]
        [InlineData("PascalCase")]
        [InlineData("")]
        [InlineData(null)]
        public void TestCreateSystemEndpointInvalidCases(string systemEndpoint)
        {
            Assert.Throws<InvalidOperationException>(() => this.endpointFactory.CreateSystemEndpoint(systemEndpoint));
        }
    }
}
