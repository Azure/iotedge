// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using Serilog;

    public class MqttBrokerUtil
    {
        static readonly string[] OnlyIotHubOperationPermissions = new[] { "mqtt:connect" };
        static readonly string[] AllOperationPermissions = new[] { "mqtt:connect", "mqtt:publish", "mqtt:subscribe" };

        public static Action<EdgeConfigBuilder> BuildAddBrokerToDeployment(bool onlyIotHubTopics)
        {
            string[] permissions;
            if (onlyIotHubTopics)
            {
                permissions = OnlyIotHubOperationPermissions;
            }
            else
            {
                permissions = AllOperationPermissions;
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
                            ["mqttBroker"] = new
                            {
                                authorizations = new[]
                                {
                            new
                            {
                                 identities = new[] { "{{iot:identity}}" },
                                 allow = new[]
                                 {
                                     new
                                     {
                                         operations = permissions
                                     }
                                 }
                            }
                                }
                            }
                        });
                });
        }
    }
}
