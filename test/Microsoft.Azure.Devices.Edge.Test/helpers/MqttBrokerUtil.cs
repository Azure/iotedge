// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;

    public class MqttBrokerUtil
    {
        static readonly List<Dictionary<string, object>> OnlyIotHubOperationAuthorizations = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object>
            {
                ["identities"] = new[] { "{{iot:identity}}" },
                ["allow"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["operations"] = new[] { "mqtt:connect" }
                    }
                }
            }
        };

        static readonly List<Dictionary<string, object>> AllOperationAuthorizations = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object>
            {
                ["identities"] = new[] { "{{iot:identity}}" },
                ["allow"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["operations"] = new[] { "mqtt:connect", "mqtt:publish", "mqtt:subscribe" },
                        ["resources"] = new[] { "#" }
                    }
                }
            }
        };

        public static Action<EdgeConfigBuilder> BuildAddBrokerToDeployment(bool onlyIotHubTopics)
        {
            var authorizations = new List<Dictionary<string, object>> { };
            if (onlyIotHubTopics)
            {
                authorizations = OnlyIotHubOperationAuthorizations;
            }
            else
            {
                authorizations = AllOperationAuthorizations;
            }

            return new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.GetModule(ModuleName.EdgeHub)
                        .WithEnvironment(new[]
                        {
                    ("experimentalFeatures__enabled", "true"),
                    ("experimentalFeatures__mqttBrokerEnabled", "true"),
                        })
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["mqttBroker"] = new Dictionary<string, object>
                            {
                                ["authorizations"] = authorizations
                            }
                        });
                });
        }
    }
}
