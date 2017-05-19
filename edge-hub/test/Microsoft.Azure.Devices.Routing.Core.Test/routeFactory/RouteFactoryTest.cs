// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.RouteFactory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Test.Endpoints;
    using Moq;
    using Xunit;

    [Unit]
    public class RouteFactoryTest
    {
        static IEnumerable<object[]> GetRouteFactoryData()
        {
            var testData = new List<object[]>();
            testData.Add(new object[]
            {
                @"FROM /messages/modules/adapter INTO brokeredEndpoint(""/modules/alertLogic/inputs/in"")",
                MessageSource.Telemetry,
                "true",
                "FunctionEndpoint"
            });

            testData.Add(new object[]
            {
                "FROM /messages/modules/alertLogic/outputs/alerts INTO $upstream",
                MessageSource.Telemetry,
                "true",
                "SystemEndpoint"
            });

            testData.Add(new object[]
            {
                @"FROM /messages/modules/alertLogic/outputs/commands WHERE alertType = ""shutdown"" INTO brokeredEndpoint(""/modules/adapter/inputs/write"")",
                MessageSource.Telemetry,
                @"alertType = ""shutdown""",
                "FunctionEndpoint"
            });

            testData.Add(new object[]
            {
                @"FROM /messages/modules/adapter INTO brokeredEndpoint(""/modules/alertLogic/inputs/in1"");",
                MessageSource.Telemetry,
                "true",
                "FunctionEndpoint"
            });

            testData.Add(new object[]
            {
                @"FROM /messages/modules/AsaModule/outputs/aggregates WHERE messageType = ""alert"" INTO $upstream",
                MessageSource.Telemetry,
                @"messageType = ""alert""",
                "SystemEndpoint"
            });

            return testData;
        }

        static IEnumerable<object[]> GetFunctionEndpointParserData()
        {
            var testData = new List<object[]>();
            testData.Add(new object[]
            {
                @"FROM /messages/modules/adapter INTO brokeredEndpoint(""/modules/alertLogic/inputs/in"")",
                "/messages/modules/adapter",
                "true",
                "brokeredEndpoint",
                "/modules/alertLogic/inputs/in"
            });

            testData.Add(new object[]
            {
                @"FROM /messages/modules/alertLogic/outputs/commands WHERE alertType = ""shutdown"" INTO brokeredEndpoint(""/modules/adapter/inputs/write"")",
                "/messages/modules/alertLogic/outputs/commands",
                @"alertType = ""shutdown""",
                "brokeredEndpoint",
                "/modules/adapter/inputs/write"
            });

            testData.Add(new object[]
            {
                @"FROM /messages/events INTO brokeredEndpoint(""/modules/alertLogic/inputs/in1"");",
                "/messages/events",
                "true",
                "brokeredEndpoint",
                "/modules/alertLogic/inputs/in1"
            });

            return testData;
        }

        static IEnumerable<object[]> GetSystemEndpointParserData()
        {
            var testData = new List<object[]>();

            testData.Add(new object[]
            {
                "FROM /messages/* INTO $upstream",
                "/messages/*",
                "true",
                "$upstream"
            });

            testData.Add(new object[]
            {
                @"FROM /messages/modules/AsaModule/outputs/aggregates WHERE messageType = ""alert"" INTO $upstream",
                "/messages/modules/AsaModule/outputs/aggregates",
                @"messageType = ""alert""",
                "$upstream"
            });

            return testData;
        }

        [MemberData(nameof(GetRouteFactoryData))]
        [Theory]
        public void TestCreate(string routeString, MessageSource messageSource, string condition, string endpointName)
        {
            var mockEndpointFactory = new Mock<IEndpointFactory>();
            mockEndpointFactory.Setup(s => s.CreateSystemEndpoint(It.IsAny<string>())).Returns(new TestEndpoint("SystemEndpoint"));
            mockEndpointFactory.Setup(s => s.CreateFunctionEndpoint(It.IsAny<string>(), It.IsAny<string>())).Returns(new TestEndpoint("FunctionEndpoint"));
            var routeFactory = new TestRouteFactory(mockEndpointFactory.Object);

            Route route = routeFactory.Create(routeString);
            Assert.NotNull(route);

            Assert.Equal(messageSource, route.Source);
            Assert.Equal(condition, route.Condition);
            Assert.Equal(1, route.Endpoints.Count);
            Endpoint endpoint = route.Endpoints.First();
            Assert.NotNull(endpoint);
            Assert.Equal(endpointName, endpoint.Name);
        }

        [MemberData(nameof(GetFunctionEndpointParserData))]
        [Theory]
        public void TestParseRouteWithFunctionEndpoint(string routeString, string expectedSource, string expectedCondition, string function, string inputEndpoint)
        {
            var mockEndpointFactory = new Mock<IEndpointFactory>();
            mockEndpointFactory.Setup(ef => ef.CreateFunctionEndpoint(
                It.Is<string>(s => s.Equals(function, StringComparison.OrdinalIgnoreCase)),
                It.Is<string>(s => s.Equals(inputEndpoint, StringComparison.OrdinalIgnoreCase))))
                .Returns(new TestEndpoint(function));
            var routeFactory = new TestRouteFactory(mockEndpointFactory.Object);

            routeFactory.ParseRoute(routeString, out string messageSource, out string condition, out Endpoint endpoint);

            Assert.NotNull(messageSource);
            Assert.Equal(expectedSource, messageSource);

            Assert.NotNull(condition);
            Assert.Equal(expectedCondition, condition);

            Assert.NotNull(endpoint);
            Assert.Equal(function, endpoint.Name);
        }

        [MemberData(nameof(GetSystemEndpointParserData))]
        [Theory]
        public void TestParseRouteWithSystemEndpoint(string routeString, string expectedSource, string expectedCondition, string systemEndpoint)
        {
            var mockEndpointFactory = new Mock<IEndpointFactory>();
            mockEndpointFactory.Setup(ef => ef.CreateSystemEndpoint(
                    It.Is<string>(s => s.Equals(systemEndpoint, StringComparison.OrdinalIgnoreCase))))
                .Returns(new TestEndpoint(systemEndpoint));
            var routeFactory = new TestRouteFactory(mockEndpointFactory.Object);

            routeFactory.ParseRoute(routeString, out string messageSource, out string condition, out Endpoint endpoint);

            Assert.NotNull(messageSource);
            Assert.Equal(expectedSource, messageSource);

            Assert.NotNull(condition);
            Assert.Equal(expectedCondition, condition);

            Assert.NotNull(endpoint);
            Assert.Equal(systemEndpoint, endpoint.Name);
        }
    }
}