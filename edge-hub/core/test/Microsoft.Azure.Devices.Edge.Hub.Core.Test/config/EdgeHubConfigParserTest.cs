// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Config
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Moq;
    using Xunit;

    [Unit]
    public class EdgeHubConfigParserTest
    {
        [Fact]
        public void GetEdgeHubConfig_ValidInput_MappingIsCorrect()
        {
            var validator = new Mock<BrokerPropertiesValidator>();
            validator
                .Setup(v => v.ValidateAuthorizationConfig(It.IsAny<AuthorizationProperties>()))
                .Returns(new List<string>());

            var routeFactory = new EdgeRouteFactory(new Mock<IEndpointFactory>().Object);
            var configParser = new EdgeHubConfigParser(routeFactory, validator.Object);

            EdgeHubDesiredProperties properties = ConfigTestData.GetTestData();

            // act
            EdgeHubConfig result = configParser.GetEdgeHubConfig(properties);

            // assert
            validator.Verify(v => v.ValidateAuthorizationConfig(properties.BrokerConfiguration.Authorizations), Times.Once());

            Assert.Equal("1.2.0", result.SchemaVersion);
            AuthorizationConfig authzConfig = result
                .BrokerConfiguration
                .Expect(() => new InvalidOperationException("missing broker config"))
                .Authorizations
                .Expect(() => new InvalidOperationException("missing authorization config"));

            Assert.Equal(3, authzConfig.Statements.Count);

            var result0 = authzConfig.Statements[0];
            Assert.Equal(Effect.Deny, result0.Effect);
            Assert.Equal(2, result0.Identities.Count);
            Assert.Equal("device_1", result0.Identities[0]);
            Assert.Equal("device_3", result0.Identities[1]);
            Assert.Equal(1, result0.Operations.Count);
            Assert.Equal("mqtt:publish", result0.Operations[0]);
            Assert.Equal(2, result0.Resources.Count);
            Assert.Equal("system/alerts/+", result0.Resources[0]);
            Assert.Equal("core/#", result0.Resources[1]);

            var result1 = authzConfig.Statements[1];
            Assert.Equal(Effect.Allow, result1.Effect);
            Assert.Equal(2, result1.Identities.Count);
            Assert.Equal("device_1", result1.Identities[0]);
            Assert.Equal("device_3", result1.Identities[1]);
            Assert.Equal(2, result1.Operations.Count);
            Assert.Equal("mqtt:publish", result1.Operations[0]);
            Assert.Equal("mqtt:subscribe", result1.Operations[1]);
            Assert.Equal(2, result1.Resources.Count);
            Assert.Equal("topic/a", result1.Resources[0]);
            Assert.Equal("topic/b", result1.Resources[1]);

            var result2 = authzConfig.Statements[2];
            Assert.Equal(Effect.Allow, result2.Effect);
            Assert.Equal(1, result2.Identities.Count);
            Assert.Equal("device_2", result2.Identities[0]);
            Assert.Equal(2, result2.Operations.Count);
            Assert.Equal("mqtt:publish", result2.Operations[0]);
            Assert.Equal("mqtt:subscribe", result2.Operations[1]);
            Assert.Equal(2, result2.Resources.Count);
            Assert.Equal("topic1", result2.Resources[0]);
            Assert.Equal("topic2", result2.Resources[1]);
        }

        [Fact]
        public void GetEdgeHubConfig_ValidatorReturnsError_ExpectedException()
        {
            var validator = new Mock<BrokerPropertiesValidator>();
            validator
                .Setup(v => v.ValidateAuthorizationConfig(It.IsAny<AuthorizationProperties>()))
                .Returns(new List<string> { "Validation error has occurred" });

            var routeFactory = new EdgeRouteFactory(new Mock<IEndpointFactory>().Object);
            var configParser = new EdgeHubConfigParser(routeFactory, validator.Object);

            var brokerProperties = new BrokerProperties(new BridgeConfig(), new AuthorizationProperties());
            var properties = new EdgeHubDesiredProperties(
                "1.2.0",
                new Dictionary<string, RouteConfiguration>(),
                new StoreAndForwardConfiguration(100),
                brokerProperties);

            // assert
            Assert.Throws<InvalidOperationException>(() => configParser.GetEdgeHubConfig(properties));
        }
    }
}
