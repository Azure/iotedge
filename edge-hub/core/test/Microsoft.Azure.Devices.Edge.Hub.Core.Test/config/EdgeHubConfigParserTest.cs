// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Config
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class EdgeHubConfigParserTest
    {
        [Theory]
        [MemberData(nameof(GetSchemaVersionData))]
        public void SchemaVersionCheckTest(string manifest, Type expectedException)
        {
            var validator = new Mock<BrokerPropertiesValidator>();
            validator
                .Setup(v => v.ValidateAuthorizationConfig(It.IsAny<AuthorizationProperties>()))
                .Returns(new List<string>());
            validator
                .Setup(v => v.ValidateBridgeConfig(It.IsAny<BridgeConfig>()))
                .Returns(new List<string>());

            var endpointFactory = new Mock<IEndpointFactory>();
            endpointFactory
                .Setup(v => v.CreateSystemEndpoint(It.IsAny<string>()))
                .Returns(new NullEndpoint("$upstream"));

            var routeFactory = new EdgeRouteFactory(endpointFactory.Object);
            var configParser = new EdgeHubConfigParser(routeFactory, validator.Object);

            if (expectedException != null)
            {
                Assert.Throws(expectedException, () => configParser.GetEdgeHubConfig(manifest));
            }
            else
            {
                configParser.GetEdgeHubConfig(manifest);
            }
        }

        public static IEnumerable<object[]> GetSchemaVersionData()
        {
            string noVersion =
                @"{
                  'routes': {
                    'route1': 'FROM /* INTO $upstream'
                  },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";

            string version_0_1 =
                    @"{
                      'schemaVersion': '0.1',
                      'routes': {
                        'route1': 'FROM /* INTO $upstream'
                      },
                      'storeAndForwardConfiguration': {
                        'timeToLiveSecs': 20
                      },
                      '$version': 2
                    }";

            string version_1 =
                    @"{
                      'schemaVersion': '1',
                      'routes': {
                        'route1': 'FROM /* INTO $upstream'
                      },
                      'storeAndForwardConfiguration': {
                        'timeToLiveSecs': 20
                      },
                      '$version': 2
                    }";

            string version_1_0 =
                    @"{
                      'schemaVersion': '1.0',
                      'routes': {
                        'route1': 'FROM /* INTO $upstream'
                      },
                      'storeAndForwardConfiguration': {
                        'timeToLiveSecs': 20
                      },
                      '$version': 2
                    }";

            string version_1_1 =
                    @"{
                      'schemaVersion': '1.1',
                      'routes': {
                        'route1': 'FROM /* INTO $upstream'
                      },
                      'storeAndForwardConfiguration': {
                        'timeToLiveSecs': 20
                      },
                      '$version': 2
                    }";

            string version_1_1_0 =
                    @"{
                      'schemaVersion': '1.1.0',
                      'routes': {
                        'route1': 'FROM /* INTO $upstream'
                      },
                      'storeAndForwardConfiguration': {
                        'timeToLiveSecs': 20
                      },
                      '$version': 2
                    }";

            string version_1_2 =
                    @"{
                      'schemaVersion': '1.2',
                      'routes': {
                        'route1': 'FROM /* INTO $upstream'
                      },
                      'storeAndForwardConfiguration': {
                        'timeToLiveSecs': 20
                      },
                      'authorizations': [ ],
                      '$version': 2
                    }";

            string version_2_0 =
                    @"{
                      'schemaVersion': '2.0',
                      'routes': {
                        'route1': 'FROM /* INTO $upstream'
                      },
                      'storeAndForwardConfiguration': {
                        'timeToLiveSecs': 20
                      },
                      '$version': 2
                    }";

            string version_2_0_1 =
                    @"{
                      'schemaVersion': '2.0.1',
                      'routes': {
                        'route1': 'FROM /* INTO $upstream'
                      },
                      'storeAndForwardConfiguration': {
                        'timeToLiveSecs': 20
                      },
                      '$version': 2
                    }";

            string versionMismatch =
                @"{
                  'schemaVersion': '1.0',
                  'routes': {
                    'route1': {
                      'route': 'FROM /* INTO $upstream',
                      'priority': 1,
                      'timeToLiveSecs': 7200
                    }
                  },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";

            yield return new object[] { noVersion, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_0_1, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_1, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_1_0, null };
            yield return new object[] { version_1_1, null };
            yield return new object[] { version_1_1_0, null };
            yield return new object[] { version_1_2, null };
            yield return new object[] { version_2_0, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_2_0_1, typeof(InvalidSchemaVersionException) };
            yield return new object[] { versionMismatch, typeof(JsonReaderException) };
        }

        [Fact]
        public void GetEdgeHubConfig_ValidInput_MappingIsCorrect()
        {
            var validator = new Mock<BrokerPropertiesValidator>();
            validator
                .Setup(v => v.ValidateAuthorizationConfig(It.IsAny<AuthorizationProperties>()))
                .Returns(new List<string>());
            validator
                .Setup(v => v.ValidateBridgeConfig(It.IsAny<BridgeConfig>()))
                .Returns(new List<string>());

            var routeFactory = new EdgeRouteFactory(new Mock<IEndpointFactory>().Object);
            var configParser = new EdgeHubConfigParser(routeFactory, validator.Object);

            EdgeHubDesiredProperties_1_2 properties = ConfigTestData.GetTestData();

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
        public void GetEdgeHubConfig_AuthorizationValidatorReturnsError_ExpectedException()
        {
            var validator = new Mock<BrokerPropertiesValidator>();
            validator
                .Setup(v => v.ValidateAuthorizationConfig(It.IsAny<AuthorizationProperties>()))
                .Returns(new List<string> { "Validation error has occurred" });

            var routeFactory = new EdgeRouteFactory(new Mock<IEndpointFactory>().Object);
            var configParser = new EdgeHubConfigParser(routeFactory, validator.Object);

            var authzProperties = new AuthorizationProperties
            {
                new AuthorizationProperties.Statement(
                    identities: new List<string>
                    {
                        "device_1",
                        "device_3"
                    },
                    allow: new List<AuthorizationProperties.Rule>(),
                    deny: new List<AuthorizationProperties.Rule>())
            };

            var brokerProperties = new BrokerProperties(new BridgeConfig(), authzProperties);
            var properties = new EdgeHubDesiredProperties_1_2(
                "1.2.0",
                new Dictionary<string, RouteSpec>(),
                new StoreAndForwardConfiguration(100),
                brokerProperties);

            // assert
            Assert.Throws<InvalidOperationException>(() => configParser.GetEdgeHubConfig(properties));
        }

        [Fact]
        public void GetEdgeHubConfig_BridgeValidatorReturnsError_ExpectedException()
        {
            var validator = new Mock<BrokerPropertiesValidator>();
            validator
                .Setup(v => v.ValidateAuthorizationConfig(It.IsAny<AuthorizationProperties>()))
                .Returns(new List<string>());
            validator
                .Setup(v => v.ValidateBridgeConfig(It.IsAny<BridgeConfig>()))
                .Returns(new List<string> { "Validation error has occurred" });

            var routeFactory = new EdgeRouteFactory(new Mock<IEndpointFactory>().Object);
            var configParser = new EdgeHubConfigParser(routeFactory, validator.Object);

            var bridgeConfig = new BridgeConfig
            {
                new Bridge("floor2", new List<Settings> { })
            };

            var brokerProperties = new BrokerProperties(bridgeConfig, new AuthorizationProperties());
            var properties = new EdgeHubDesiredProperties_1_2(
                "1.2.0",
                new Dictionary<string, RouteSpec>(),
                new StoreAndForwardConfiguration(100),
                brokerProperties);

            // assert
            Assert.Throws<InvalidOperationException>(() => configParser.GetEdgeHubConfig(properties));
        }
    }
}
