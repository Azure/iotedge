// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;

    public class MqttBrokerUtil
    {
        static readonly Dictionary<string, object> OnlyIotHubOperationAuthorizations = new Dictionary<string, object>
        {
            ["identities"] = new[] { "{{iot:identity}}" },
            ["allow"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["operations"] = new[] { "mqtt:connect" }
                }
            }
        };

        static readonly Dictionary<string, object> AllOperationAuthorizations = new Dictionary<string, object>
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
        };

        public static Action<EdgeConfigBuilder> BuildAddBrokerToDeployment(bool onlyIotHubTopics)
        {
            var authorizations = new Dictionary<string, object> { };
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
                                ["authorizations"] = new Dictionary<string, object>
                                {
                                    ["identities"] = new[] { "{{iot:identity}}" },
                                    ["allow"] = new Dictionary<string, object>
                                    {
                                        ["operations"] = authorizations
                                    }
                                }
                            }
                        });
                });
        }
    }
}
