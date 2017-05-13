// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Moq;
    using Xunit;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;

    public class RouteFactoryTest
    {
        [Unit]
        [Fact]
        public void TestCreate()
        {
            var connectionManagerMock = new Mock<IConnectionManager>();
            var messageConverterMock = new Mock<Core.IMessageConverter<IRoutingMessage>>();
            var endpointFactory = new SimpleEndpointFactory(connectionManagerMock.Object, messageConverterMock.Object);
            var routeFactory = new SimpleRouteFactory(endpointFactory);

            Route route = routeFactory.Create(string.Empty);
            Assert.NotNull(route);

            Assert.Equal(MessageSource.Telemetry, route.Source);
            Assert.Equal("true", route.Condition);
            Assert.Equal(1, route.Endpoints.Count);
            Endpoint endpoint = route.Endpoints.First();
            Assert.Equal("CloudEndpoint", endpoint.Type);
            Assert.IsType<CloudEndpoint>(endpoint);
        }
    }
}