// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Config
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class EdgeHubDesiredPropertiesTest
    {
        [Fact]
        public void RoutesSmokeTest_1_0()
        {
            string normal =
                @"{
                  'schemaVersion': '1.0',
                  'routes': {
                    'route1': 'from /* INTO $upstream'
                  },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_0>(normal);
            Assert.Equal(1, desiredProperties.Routes.Count);

            Assert.Equal("from /* INTO $upstream", desiredProperties.Routes["route1"]);
            Assert.Equal(20, desiredProperties.StoreAndForwardConfiguration.TimeToLiveSecs);
        }

        [Fact]
        public void RoutesSmokeTest_1_1()
        {
            string normal =
                @"{
                  'schemaVersion': '1.1.0',
                  'routes': {
                    'route1': 'from /* INTO $upstream',
                    'route2': {
                      'route': 'from /* INTO $upstream',
                      'priority': 1,
                      'timeToLiveSecs': 7200
                    }
                  },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_1>(normal);
            Assert.Equal(2, desiredProperties.Routes.Count);

            Assert.Equal("from /* INTO $upstream", desiredProperties.Routes["route1"].Route);
            Assert.Equal(RouteFactory.DefaultPriority, desiredProperties.Routes["route1"].Priority);
            Assert.Equal(0u, desiredProperties.Routes["route1"].TimeToLiveSecs);

            Assert.Equal("from /* INTO $upstream", desiredProperties.Routes["route2"].Route);
            Assert.Equal(1u, desiredProperties.Routes["route2"].Priority);
            Assert.Equal(7200u, desiredProperties.Routes["route2"].TimeToLiveSecs);

            Assert.Equal(20, desiredProperties.StoreAndForwardConfiguration.TimeToLiveSecs);
        }

        [Fact]
        public void RoutesEmptyTest()
        {
            string emptyRoutesSection =
                @"{
                  'schemaVersion': '1.1.0',
                  'routes': {},
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_1>(emptyRoutesSection);
            Assert.Equal(0, desiredProperties.Routes.Count);
        }

        [Fact]
        public void RoutesNoPriorityTest()
        {
            string noPriority =
                @"{
                  'schemaVersion': '1.1.0',
                  'routes': {
                    'route2': {
                      'route': 'from /* INTO $upstream',
                      'timeToLiveSecs': 7200
                    }
                  },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_1>(noPriority);
            Assert.Equal(1, desiredProperties.Routes.Count);
            Assert.Equal(RouteFactory.DefaultPriority, desiredProperties.Routes["route2"].Priority);
        }

        [Fact]
        public void RoutesNoTtlTest()
        {
            string noTTL =
                @"{
                  'schemaVersion': '1.1.0',
                  'routes': {
                    'route2': {
                      'route': 'from /* INTO $upstream',
                      'priority': 1
                    }
                  },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_1>(noTTL);
            Assert.Equal(1, desiredProperties.Routes.Count);
            Assert.Equal(0u, desiredProperties.Routes["route2"].TimeToLiveSecs);
        }

        [Fact]
        public void RoutesNoPriorityOrTtlTest()
        {
            string noPriorityOrTTL =
                @"{
                  'schemaVersion': '1.1.0',
                  'routes': {
                    'route2': {
                      'route': 'from /* INTO $upstream'
                    }
                  },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_1>(noPriorityOrTTL);
            Assert.Equal(1, desiredProperties.Routes.Count);
            Assert.Equal(RouteFactory.DefaultPriority, desiredProperties.Routes["route2"].Priority);
            Assert.Equal(0u, desiredProperties.Routes["route2"].TimeToLiveSecs);
        }

        [Theory]
        [MemberData(nameof(GetMalformedData))]
        public void RoutesSectionMalformedTest(string manifest, Type expectedException)
        {
            var ex = Assert.ThrowsAny<Exception>(() => JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_1>(manifest));
            Assert.Equal(expectedException, ex.GetType());
        }

        public static IEnumerable<object[]> GetMalformedData()
        {
            string noRoutesSection =
                @"{
                  'schemaVersion': '1.1.0',
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";

            string emptyRouteName1 =
                @"{
                  'schemaVersion': '1.1.0',
                  'routes': {
                    '': 'from /* INTO $upstream',
                  },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";

            string emptyRouteName2 =
                @"{
                  'schemaVersion': '1.1.0',
                  'routes': {
                    '': {
                      'route': 'from /* INTO $upstream'
                    }
                  },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";

            string emptyRouteString1 =
                @"{
                  'schemaVersion': '1.1.0',
                  'routes': {
                    'route1': ''
                    },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";

            string emptyRouteString2 =
                @"{
                  'schemaVersion': '1.1.0',
                  'routes': {
                    'route2': {
                      'route': '',
                      'priority': 1,
                      'timeToLiveSecs': 7200
                    }
                  },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";

            string noRouteString =
                @"{
                  'schemaVersion': '1.1.0',
                  'routes': {
                    'route2': ,
                  },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";

            string badPriorityValue =
                @"{
                  'schemaVersion': '1.1.0',
                  'routes': {
                    'route1': {
                      'route': 'from /* INTO $upstream',
                      'priority': 50,
                      'timeToLiveSecs': 7200
                    }
                  },
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";

            yield return new object[] { noRoutesSection, typeof(ArgumentNullException) };
            yield return new object[] { emptyRouteName1, typeof(InvalidDataException) };
            yield return new object[] { emptyRouteName2, typeof(InvalidDataException) };
            yield return new object[] { emptyRouteString1, typeof(ArgumentException) };
            yield return new object[] { emptyRouteString2, typeof(ArgumentException) };
            yield return new object[] { noRouteString, typeof(InvalidDataException) };
            yield return new object[] { badPriorityValue, typeof(ArgumentOutOfRangeException) };
        }

        [Fact]
        public void AuthorizationsTest()
        {
            string properties =
                @"{
                  'schemaVersion': '1.2.0',
                  'routes': {},
                  'storeAndForwardConfiguration': {},
                  'mqttBroker': {
                      'authorizations': [
                          {
                              'identities': [
                                  'device_1',
                                  'device_2'
                              ],
                              'allow': [
                                  {
                                      'operations': [
                                          'mqtt:publish'
                                      ],
                                      'resources':[
                                          '/telemetry/#'
                                      ]
                                  }
                              ],
                              'deny': [
                                  {
                                      'operations': [
                                          'mqtt:subscribe'
                                      ],
                                      'resources':[
                                          '/alert/#'
                                      ]
                                  }
                              ]
                          }
                      ],
                  },
                  '$version': 2
                }";
            var props = JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_2>(properties);
            var authConfig = props.BrokerConfiguration.Authorizations;
            Assert.Single(authConfig);
            Assert.Equal(2, authConfig[0].Identities.Count);
            Assert.Equal("device_1", authConfig[0].Identities[0]);
            Assert.Equal("device_2", authConfig[0].Identities[1]);
            Assert.Single(authConfig[0].Allow);
            Assert.Single(authConfig[0].Deny);
            Assert.Single(authConfig[0].Allow[0].Operations);
            Assert.Equal("mqtt:publish", authConfig[0].Allow[0].Operations[0]);
            Assert.Single(authConfig[0].Allow[0].Resources);
            Assert.Equal("/telemetry/#", authConfig[0].Allow[0].Resources[0]);
            Assert.Single(authConfig[0].Deny[0].Operations);
            Assert.Equal("mqtt:subscribe", authConfig[0].Deny[0].Operations[0]);
            Assert.Single(authConfig[0].Deny[0].Resources);
            Assert.Equal("/alert/#", authConfig[0].Deny[0].Resources[0]);
        }

        [Fact]
        public void BridgeTest()
        {
            string properties =
                @"{
                  'schemaVersion': '1.2.0',
                  'routes': {},
                  'storeAndForwardConfiguration': {},
                  'mqttBroker': {
                      'bridges': [
                          {
                              'endpoint': '$upstream',
                              'settings': [
                                  {
                                      'direction': 'in',
                                      'topic': 'telemetry/#',
                                      'outPrefix': '/local/topic',
                                      'inPrefix': '/remote/topic'
                                  },
                                  {
                                      'direction': 'out',
                                      'topic': '',
                                      'inPrefix': '/local/telemetry',
                                      'outPrefix': '/remote/messages'
                                  }
                              ]
                          }
                      ],
                  },
                  '$version': 2
                }";

            var props = JsonConvert.DeserializeObject<EdgeHubDesiredProperties_1_2>(properties);
            var bridges = props.BrokerConfiguration.Bridges;
            Assert.Single(bridges);
            Assert.Equal("$upstream", bridges[0].Endpoint);
            Assert.Equal(2, bridges[0].Settings.Count);
            Assert.Equal(Direction.In, bridges[0].Settings[0].Direction);
            Assert.Equal("telemetry/#", bridges[0].Settings[0].Topic);
            Assert.Equal("/local/topic", bridges[0].Settings[0].OutPrefix);
            Assert.Equal("/remote/topic", bridges[0].Settings[0].InPrefix);
            Assert.Equal(Direction.Out, bridges[0].Settings[1].Direction);
            Assert.Equal(string.Empty, bridges[0].Settings[1].Topic);
            Assert.Equal("/local/telemetry", bridges[0].Settings[1].InPrefix);
            Assert.Equal("/remote/messages", bridges[0].Settings[1].OutPrefix);
        }
    }
}
