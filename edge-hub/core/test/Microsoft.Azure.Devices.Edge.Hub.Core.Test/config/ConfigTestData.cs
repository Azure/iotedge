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
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Moq;
    using Xunit;

    public class ConfigTestData
    {
        public static EdgeHubDesiredProperties_1_2 GetTestData()
        {
            var statement1 = new AuthorizationProperties.Statement(
                identities: new List<string>
                {
                    "device_1",
                    "device_3"
                },
                allow: new List<AuthorizationProperties.Rule>
                {
                    new AuthorizationProperties.Rule(
                        operations: new List<string>
                        {
                            "mqtt:publish",
                            "mqtt:subscribe"
                        },
                        resources: new List<string>
                        {
                            "topic/a",
                            "topic/b"
                        })
                },
                deny: new List<AuthorizationProperties.Rule>
                {
                    new AuthorizationProperties.Rule(
                        operations: new List<string>
                        {
                            "mqtt:publish"
                        },
                        resources: new List<string>
                        {
                            "system/alerts/+",
                            "core/#"
                        })
                });

            var statement2 = new AuthorizationProperties.Statement(
                identities: new List<string>
                {
                    "device_2"
                },
                allow: new List<AuthorizationProperties.Rule>
                {
                    new AuthorizationProperties.Rule(
                        operations: new List<string>
                        {
                            "mqtt:publish",
                            "mqtt:subscribe"
                        },
                        resources: new List<string>
                        {
                            "topic1",
                            "topic2"
                        })
                },
                deny: new List<AuthorizationProperties.Rule>());

            var authzProperties = new AuthorizationProperties { statement1, statement2 };
            var bridgeConfig = new BridgeConfig
            {
                new Bridge("$upstream", new List<Settings>
                {
                    new Settings(Direction.In, "topic/a", "local/", "remote/")
                }),
                new Bridge("floor2", new List<Settings>
                {
                    new Settings(Direction.Out, "/topic/b", "local", "remote")
                })
            };

            var brokerProperties = new BrokerProperties(bridgeConfig, authzProperties);
            var properties = new EdgeHubDesiredProperties_1_2(
                "1.2.0",
                new Dictionary<string, RouteSpec>(),
                new StoreAndForwardConfiguration(100),
                brokerProperties);

            return properties;
        }
    }
}
