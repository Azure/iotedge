// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Config
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class EdgeHubDesiredPropertiesTest
    {
        [Fact]
        public void RoutesSectionTest()
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

            string emptyRoutesSection =
                @"{
                  'schemaVersion': '1.0',
                  'routes': {},
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";
            desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(emptyRoutesSection);
            Assert.Equal(0, desiredProperties.Routes.Count);

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
            desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(noPriority);
            Assert.Equal(1, desiredProperties.Routes.Count);
            Assert.Equal(RouteFactory.DefaultPriority, desiredProperties.Routes["route2"].Priority);

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
            desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(noTTL);
            Assert.Equal(1, desiredProperties.Routes.Count);
            Assert.Equal(0u, desiredProperties.Routes["route2"].TimeToLiveSecs);

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
            desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(noPriorityOrTTL);
            Assert.Equal(1, desiredProperties.Routes.Count);
            Assert.Equal(RouteFactory.DefaultPriority, desiredProperties.Routes["route2"].Priority);
            Assert.Equal(0u, desiredProperties.Routes["route2"].TimeToLiveSecs);
        }

        [Fact]
        public void RoutesSectionMalformedTest()
        {
            string noRoutesSection =
                @"{
                  'schemaVersion': '1.0',
                  'storeAndForwardConfiguration': {
                    'timeToLiveSecs': 20
                  },
                  '$version': 2
                }";
            Assert.Throws<ArgumentNullException>(() => JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(noRoutesSection));

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
            Assert.Throws<ArgumentException>(() => JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(emptyRouteName1));

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
            Assert.Throws<ArgumentException>(() => JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(emptyRouteName2));

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
            Assert.Throws<ArgumentException>(() => JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(emptyRouteString1));

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
            Assert.Throws<ArgumentException>(() => JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(emptyRouteString2));

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
            Assert.Throws<ArgumentException>(() => JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(noRouteString));
        }
    }
}
