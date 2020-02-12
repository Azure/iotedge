// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Config
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Newtonsoft.Json;
    using Xunit;
    using Xunit.Sdk;

    [Unit]
    public class EdgeHubDesiredPropertiesTest
    {
        [Fact]
        public void RoutesSmokeTest()
        {
            string normal =
                @"{
                  'schemaVersion': '1.0',
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
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(normal);
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
                  'schemaVersion': '1.0',
                  'routes': {},
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(emptyRoutesSection);
            Assert.Equal(0, desiredProperties.Routes.Count);
        }

        [Fact]
        public void RoutesNoPriorityTest()
        {
            string noPriority =
                @"{
                  'schemaVersion': '1.0',
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
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(noPriority);
            Assert.Equal(1, desiredProperties.Routes.Count);
            Assert.Equal(RouteFactory.DefaultPriority, desiredProperties.Routes["route2"].Priority);
        }

        [Fact]
        public void RoutesNoTtlTest()
        {
            string noTTL =
                @"{
                  'schemaVersion': '1.0',
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
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(noTTL);
            Assert.Equal(1, desiredProperties.Routes.Count);
            Assert.Equal(0u, desiredProperties.Routes["route2"].TimeToLiveSecs);
        }

        [Fact]
        public void RoutesNoPriorityOrTtlTest()
        {
            string noPriorityOrTTL =
                @"{
                  'schemaVersion': '1.0',
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
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(noPriorityOrTTL);
            Assert.Equal(1, desiredProperties.Routes.Count);
            Assert.Equal(RouteFactory.DefaultPriority, desiredProperties.Routes["route2"].Priority);
            Assert.Equal(0u, desiredProperties.Routes["route2"].TimeToLiveSecs);
        }

        [Theory]
        [MemberData(nameof(GetMalformedData))]
        public void RoutesSectionMalformedTest(string manifest, Type expectedException)
        {
            var ex = Assert.ThrowsAny<Exception>(() => JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(manifest));
            Assert.Equal(expectedException, ex.GetType());
        }

        public static IEnumerable<object[]> GetMalformedData()
        {
            string noRoutesSection =
                @"{
                  'schemaVersion': '1.0',
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";

            string emptyRouteName1 =
                @"{
                  'schemaVersion': '1.0',
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
                  'schemaVersion': '1.0',
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
                  'schemaVersion': '1.0',
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
                  'schemaVersion': '1.0',
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
                  'schemaVersion': '1.0',
                  'routes': {
                    'route2': ,
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
        }
    }
}
