// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Moq;
    using Xunit;

    [Unit]
    public class EdgeHubConfigTest
    {
        [Fact]
        public void ApplyPatchTest()
        {
            string schemaVersion = "2.0";
            var route1 = new Route("r1", "", "iothub1", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            var route2 = new Route("r2", "", "iothub1", TelemetryMessageSource.Instance, new HashSet<Endpoint>()); ;
            var route3 = new Route("r3", "", "iothub1", TelemetryMessageSource.Instance, new HashSet<Endpoint>()); ;
            var route4 = new Route("r4", "", "iothub1", TelemetryMessageSource.Instance, new HashSet<Endpoint>()); ;

            var baseRoutes = new Dictionary<string, Route>
            {
                ["route1"] = route1,
                ["route2"] = route2
            };
            var baseStoreAndForwardConfiguration = new StoreAndForwardConfiguration(-1);
            var baseConfig = new EdgeHubConfig(schemaVersion, baseRoutes, baseStoreAndForwardConfiguration);
            ValidateConfig(
                baseConfig,
                new Dictionary<string, Route>
                {
                    ["route1"] = route1,
                    ["route2"] = route2
                },
                TimeSpan.MaxValue);

            var patch1Routes = new Dictionary<string, Route>
            {
                ["route1"] = route2,
                ["route3"] = route3
            };
            var patch1StoreAndForwardConfiguration = new StoreAndForwardConfiguration(200);
            var patch1Config = new EdgeHubConfig(schemaVersion, patch1Routes, patch1StoreAndForwardConfiguration);
            baseConfig.ApplyDiff(patch1Config);
            ValidateConfig(
                baseConfig,
                new Dictionary<string, Route>
                {
                    ["route1"] = route2,
                    ["route3"] = route3,
                    ["route2"] = route2
                },
                TimeSpan.FromSeconds(200));

            var patch2Routes = new Dictionary<string, Route>
            {
                ["route2"] = route4,
            };
            var patch2Config = new EdgeHubConfig(schemaVersion, patch2Routes, null);
            baseConfig.ApplyDiff(patch2Config);
            ValidateConfig(
                baseConfig,
                new Dictionary<string, Route>
                {
                    ["route1"] = route2,
                    ["route3"] = route3,
                    ["route2"] = route4
                },
                TimeSpan.FromSeconds(200));

            var patch3StoreAndForwardConfiguration = new StoreAndForwardConfiguration(300);
            var patch3Config = new EdgeHubConfig(schemaVersion, null, patch3StoreAndForwardConfiguration);
            baseConfig.ApplyDiff(patch3Config);
            ValidateConfig(
                baseConfig,
                new Dictionary<string, Route>
                {
                    ["route1"] = route2,
                    ["route3"] = route3,
                    ["route2"] = route4
                },
                TimeSpan.FromSeconds(300));

            var patch4Config = new EdgeHubConfig(schemaVersion, null, null);
            baseConfig.ApplyDiff(patch3Config);
            ValidateConfig(
                baseConfig,
                new Dictionary<string, Route>
                {
                    ["route1"] = route2,
                    ["route3"] = route3,
                    ["route2"] = route4
                },
                TimeSpan.FromSeconds(300));

            var patch5Config = new EdgeHubConfig("3.0", null, null);
            Assert.Throws<InvalidOperationException>(() => baseConfig.ApplyDiff(patch5Config));
        }

        void ValidateConfig(EdgeHubConfig edgeHubConfig, IDictionary<string, Route> expectedRoutes, TimeSpan expectedTimeSpan)
        {
            Assert.NotNull(edgeHubConfig);
            Assert.Equal(expectedRoutes.Count, edgeHubConfig.Routes.Count);
            foreach(KeyValuePair<string, Route> expectedRoute in expectedRoutes)
            {
                Assert.Equal(expectedRoute.Value, edgeHubConfig.Routes[expectedRoute.Key]);
            }
            Assert.Equal(edgeHubConfig.StoreAndForwardConfiguration.TimeToLive, expectedTimeSpan);
        }
    }
}
