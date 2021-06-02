// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.RouteFactory
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Query;
    using Microsoft.Azure.Devices.Routing.Core.Test.Endpoints;
    using Moq;
    using Xunit;

    [Unit]
    public class RouteFactoryTest
    {
        [MemberData(nameof(GetFunctionEndpointParserData))]
        [Theory]
        public void TestParseRouteWithFunctionEndpoint(string routeString, IMessageSource expectedSource, string expectedCondition, string function, string inputEndpoint)
        {
            var mockEndpointFactory = new Mock<IEndpointFactory>(MockBehavior.Strict);
            mockEndpointFactory.Setup(
                    ef => ef.CreateFunctionEndpoint(
                        It.Is<string>(s => s.Equals(function, StringComparison.OrdinalIgnoreCase)),
                        It.Is<string>(s => s.Equals(inputEndpoint, StringComparison.OrdinalIgnoreCase))))
                .Returns(new TestEndpoint(function));
            var routeFactory = new TestRouteFactory(mockEndpointFactory.Object);

            routeFactory.ParseRoute(routeString, out IMessageSource messageSource, out string condition, out Endpoint endpoint);

            Assert.NotNull(messageSource);
            Assert.True(expectedSource.Equals(messageSource));

            Assert.NotNull(condition);
            Assert.Equal(expectedCondition, condition);

            Assert.NotNull(endpoint);
            Assert.Equal(function, endpoint.Name);
        }

        [MemberData(nameof(GetSystemEndpointParserData))]
        [Theory]
        public void TestParseRouteWithSystemEndpoint(string routeString, IMessageSource expectedSource, string expectedCondition, string systemEndpoint)
        {
            var mockEndpointFactory = new Mock<IEndpointFactory>(MockBehavior.Strict);
            mockEndpointFactory.Setup(
                    ef => ef.CreateSystemEndpoint(
                        It.Is<string>(s => s.Equals(systemEndpoint, StringComparison.OrdinalIgnoreCase))))
                .Returns(new TestEndpoint(systemEndpoint));
            var routeFactory = new TestRouteFactory(mockEndpointFactory.Object);

            routeFactory.ParseRoute(routeString, out IMessageSource messageSource, out string condition, out Endpoint endpoint);

            Assert.NotNull(messageSource);
            Assert.True(expectedSource.Equals(messageSource));

            Assert.NotNull(condition);
            Assert.Equal(expectedCondition, condition);

            Assert.NotNull(endpoint);
            Assert.Equal(systemEndpoint, endpoint.Name);
        }

        [Theory]
        [InlineData(@"FORM /messages/modules/adapter INTO brokeredEndpoint(""/modules/alertLogic/inputs/in"")")]
        [InlineData(@"SELECT FROM /messages/modules/adapter INTO brokeredEndpoint(""/modules/alertLogic/inputs/in"")")]
        [InlineData(@"FROM INTO brokeredEndpoint(""/modules/alertLogic/inputs/in"")")]
        [InlineData(@"FROM /* INTO brokeredEndpoint(""/modules/alertLogic/inputs/in')")]
        [InlineData(@"FROM /* INTO brokeredEndpoint('/modules/alertLogic/inputs/in"")")]
        [InlineData(@"FROM /* INTO brokeredEndpoint(""""/modules/alertLogic/inputs/in"""")")]
        [InlineData(@"FROM /* INTO brokeredEndpoint(''/modules/alertLogic/inputs/in'')")]
        [InlineData(@"FROM /* INTO brokeredEndpoint('""/modules/alertLogic/inputs/in'"")")]
        [InlineData(@"FROM /* INTO brokeredEndpoint(""'/modules/alertLogic/inputs/in""')")]
        [InlineData(@"FROM /messages WHERE temp == 100 INTO brokeredEndpoint(""/modules/alertLogic/inputs/in"")")]
        [InlineData(@"FROM /messages WHERE INTO brokeredEndpoint(""/modules/alertLogic/inputs/in"")")]
        [InlineData(@"FROM /messages WHERE temp = 'high' INTO")]
        [InlineData(@"FROM /messages INTO brokeredEndpoint(""/modules/alertLogic/inputs/in""")]
        [InlineData(@"FROM /messages INTO brokeredEndpoint(""/modules/alertLogic/inputs/in)")]
        [InlineData(@"FROM /messages INTO brokeredEndpoint ""/modules/alertLogic/inputs/in"")")]
        [InlineData(@"FROM /messages INTO brokeredEndpoint(/modules/alertLogic/inputs/in"")")]
        [InlineData(@"FROM /messages INTO ""/modules/alertLogic/inputs/in""")]
        [InlineData(@"FROM /messages INTO /modules/alertLogic/inputs/in")]
        [InlineData(@"FROM /messages INTO upstream")]
        public void TestInvalidRoutes(string routeString)
        {
            var mockEndpointFactory = new Mock<IEndpointFactory>();
            mockEndpointFactory.Setup(s => s.CreateSystemEndpoint(It.IsAny<string>())).Returns(new TestEndpoint("SystemEndpoint"));
            mockEndpointFactory.Setup(s => s.CreateFunctionEndpoint(It.IsAny<string>(), It.IsAny<string>())).Returns(new TestEndpoint("FunctionEndpoint"));
            var routeFactory = new TestRouteFactory(mockEndpointFactory.Object);

            Assert.Throws<RouteCompilationException>(() => routeFactory.Create(routeString));
        }

        public static IEnumerable<object[]> GetFunctionEndpointParserData()
        {
            var testData = new List<object[]>();

            testData.Add(
                new object[]
                {
                    @"FROM /* INTO brokeredEndpoint(""/modules/alertLogic/inputs/in"")",
                    CustomMessageSource.Create("/"),
                    "true",
                    "brokeredEndpoint",
                    "/modules/alertLogic/inputs/in"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /* INTO brokeredEndpoint('/modules/alertLogic/inputs/in')",
                    CustomMessageSource.Create("/"),
                    "true",
                    "brokeredEndpoint",
                    "/modules/alertLogic/inputs/in"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /* INTO brokeredEndpoint('/modules/alert""Logic/inputs/in')",
                    CustomMessageSource.Create("/"),
                    "true",
                    "brokeredEndpoint",
                    @"/modules/alert""Logic/inputs/in"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /* INTO brokeredEndpoint(""/modules/alert'Logic/inputs/in"")",
                    CustomMessageSource.Create("/"),
                    "true",
                    "brokeredEndpoint",
                    "/modules/alert'Logic/inputs/in"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /messages/modules/adapter INTO brokeredEndpoint(""/modules/alertLogic/inputs/in"")",
                    CustomMessageSource.Create("/messages/modules/adapter"),
                    "true",
                    "brokeredEndpoint",
                    "/modules/alertLogic/inputs/in"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /messages/modules/alertLogic/outputs/commands INTO brokeredEndpoint(""/modules/adapter/inputs/write"")",
                    CustomMessageSource.Create("/messages/modules/alertLogic/outputs/commands"),
                    @"true",
                    "brokeredEndpoint",
                    "/modules/adapter/inputs/write"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /messages INTO brokeredEndpoint(""/modules/alertLogic/inputs/in1"")",
                    CustomMessageSource.Create("/messages"),
                    "true",
                    "brokeredEndpoint",
                    "/modules/alertLogic/inputs/in1"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /messages/modules WHERE messageType = ""alert"" INTO brokeredEndpoint(""/modules/alertLogic/inputs/in"")",
                    CustomMessageSource.Create("/messages/modules/"),
                    @"messageType = ""alert""",
                    "brokeredEndpoint",
                    "/modules/alertLogic/inputs/in"
                });

            testData.Add(
                new object[]
                {
                    @"   FROM /messages/modules/adapter WHERE messageType = ""alert"" AND alert = ""imp"" INTO brokeredEndpoint(""/a/b/c/d"")   ",
                    CustomMessageSource.Create("/messages/modules/adapter"),
                    @"messageType = ""alert"" AND alert = ""imp""",
                    "brokeredEndpoint",
                    "/a/b/c/d"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /messages/modules/alertLogic/outputs/commands WHERE messageType = ""alert"" AND ( temp > 100 OR humidity = 50 OR place = ""basement"") INTO brokeredEndpoint(""/modules/adapter/inputs/write"")",
                    CustomMessageSource.Create("/messages/modules/alertLogic/outputs/commands"),
                    @"messageType = ""alert"" AND ( temp > 100 OR humidity = 50 OR place = ""basement"")",
                    "brokeredEndpoint",
                    "/modules/adapter/inputs/write"
                });

            testData.Add(
                new object[]
                {
                    @"  FROM /messages WHERE    messageType = ""alert"" AND humidity = 'high' AND temp >= 200 INTO brokeredEndpoint(""/modules/alertLogic/inputs/in1"")",
                    CustomMessageSource.Create("/messages"),
                    @"messageType = ""alert"" AND humidity = 'high' AND temp >= 200",
                    "brokeredEndpoint",
                    "/modules/alertLogic/inputs/in1"
                });

            testData.Add(
                new object[]
                {
                    @" SELECT * FROM /messages WHERE    messageType = ""alert"" AND humidity = 'high' AND temp >= 200 INTO brokeredEndpoint(""/modules/alertLogic/inputs/in1"")",
                    CustomMessageSource.Create("/messages"),
                    @"messageType = ""alert"" AND humidity = 'high' AND temp >= 200",
                    "brokeredEndpoint",
                    "/modules/alertLogic/inputs/in1"
                });

            return testData;
        }

        public static IEnumerable<object[]> GetSystemEndpointParserData()
        {
            var testData = new List<object[]>();

            testData.Add(
                new object[]
                {
                    @"FROM /* INTO $upstream",
                    CustomMessageSource.Create("/"),
                    "true",
                    "$upstream"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /messages/modules/adapter INTO $upstream",
                    CustomMessageSource.Create("/messages/modules/adapter"),
                    "true",
                    "$upstream"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /messages/modules/alertLogic/outputs/commands INTO $upstream",
                    CustomMessageSource.Create("/messages/modules/alertLogic/outputs/commands"),
                    @"true",
                    "$upstream"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /messages INTO $upstream",
                    CustomMessageSource.Create("/messages"),
                    "true",
                    "$upstream"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /messages/modules WHERE messageType = ""alert"" INTO $upstream",
                    CustomMessageSource.Create("/messages/modules"),
                    @"messageType = ""alert""",
                    "$upstream"
                });

            testData.Add(
                new object[]
                {
                    @"   FROM /messages/modules/adapter WHERE messageType = ""alert"" AND alert = ""imp"" INTO $upstream   ",
                    CustomMessageSource.Create("/messages/modules/adapter"),
                    @"messageType = ""alert"" AND alert = ""imp""",
                    "$upstream"
                });

            testData.Add(
                new object[]
                {
                    @"FROM /messages/modules/alertLogic/outputs/commands WHERE messageType = ""alert"" AND ( temp > 100 OR humidity = 50 OR place = ""basement"") INTO $upstream",
                    CustomMessageSource.Create("/messages/modules/alertLogic/outputs/commands"),
                    @"messageType = ""alert"" AND ( temp > 100 OR humidity = 50 OR place = ""basement"")",
                    "$upstream"
                });

            testData.Add(
                new object[]
                {
                    @"  FROM /messages WHERE    messageType = ""alert"" AND humidity = 'high' AND temp >= 200 INTO $upstream",
                    CustomMessageSource.Create("/messages"),
                    @"messageType = ""alert"" AND humidity = 'high' AND temp >= 200",
                    "$upstream"
                });

            testData.Add(
                new object[]
                {
                    @" SELECT * FROM /messages WHERE    messageType = ""alert"" AND humidity = 'high' AND temp >= 200 INTO $upstream",
                    CustomMessageSource.Create("/messages"),
                    @"messageType = ""alert"" AND humidity = 'high' AND temp >= 200",
                    "$upstream"
                });

            return testData;
        }
    }
}
