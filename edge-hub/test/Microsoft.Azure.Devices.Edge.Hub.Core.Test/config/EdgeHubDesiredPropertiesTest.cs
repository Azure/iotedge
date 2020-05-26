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
        [Theory]
        [MemberData(nameof(GetSchemaVersionData))]
        public void SchemaVersionCheckTest(string manifest, Type expectedException)
        {
            if (expectedException != null)
            {
                Assert.Throws(expectedException, () => JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(manifest));
            }
            else
            {
                JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(manifest);
            }
        }

        public static IEnumerable<object[]> GetSchemaVersionData()
        {
            string noVersion =
                @"{
                  'routes': {
                    'route1': 'from /* INTO $upstream'
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
                        'route1': 'from /* INTO $upstream'
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
                        'route1': 'from /* INTO $upstream'
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
                        'route1': 'from /* INTO $upstream'
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
                        'route1': 'from /* INTO $upstream'
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
                        'route1': 'from /* INTO $upstream'
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
                        'route1': 'from /* INTO $upstream'
                      },
                      'storeAndForwardConfiguration': {
                        'timeToLiveSecs': 20
                      },
                      '$version': 2
                    }";

            string version_2_0 =
                    @"{
                      'schemaVersion': '2.0',
                      'routes': {
                        'route1': 'from /* INTO $upstream'
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
                        'route1': 'from /* INTO $upstream'
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

            yield return new object[] { noVersion, typeof(ArgumentException) };
            yield return new object[] { version_0_1, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_1, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_1_0, null };
            yield return new object[] { version_1_1, null };
            yield return new object[] { version_1_1_0, null };
            yield return new object[] { version_1_2, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_2_0, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_2_0_1, typeof(InvalidSchemaVersionException) };
            yield return new object[] { versionMismatch, typeof(InvalidSchemaVersionException) };
        }

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
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(normal);
            Assert.Equal(1, desiredProperties.Routes.Count);

            Assert.Equal("from /* INTO $upstream", desiredProperties.Routes["route1"].Route);
            Assert.Equal(20, desiredProperties.StoreAndForwardConfiguration.TimeToLiveSecs);
        }

        [Fact]
        public void RoutesSmokeTest_1_1_0()
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
                  'schemaVersion': '1.1.0',
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
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(noPriority);
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
            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(noTTL);
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
    }
}
