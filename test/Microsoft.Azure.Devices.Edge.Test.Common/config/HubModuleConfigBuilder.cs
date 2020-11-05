// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public class HubModuleConfigBuilder : ModuleConfigBuilder
    {
        const string DefaultImage = "mcr.microsoft.com/azureiotedge-hub:1.0";
        const string HubCreateOptions = "{\"HostConfig\":{\"PortBindings\":{\"8883/tcp\":[{\"HostPort\":\"8883\"}],\"443/tcp\":[{\"HostPort\":\"443\"}],\"5671/tcp\":[{\"HostPort\":\"5671\"}]}}}";

        public HubModuleConfigBuilder(Option<string> image, bool optimizeForPerformance)
            : base(ModuleName.EdgeHub, image.GetOrElse(DefaultImage), Option.Some(HubCreateOptions))
        {
            this.WithDesiredProperties(
                new Dictionary<string, object>
                {
                    ["schemaVersion"] = "1.1",
                    ["routes"] = new { route1 = "from /* INTO $upstream" },
                    ["storeAndForwardConfiguration"] = new { timeToLiveSecs = 7200 },
                    ["mqttBroker"] = new Dictionary<string, object>
                    {
                        ["authorizations"] = new
                        {
                            identities = "{{iot:identity}}",
                            allow = new { operations = "mqtt:connect" }
                        }
                    }
                });

            if (!optimizeForPerformance)
            {
                this.WithEnvironment(new[] { ("OptimizeForPerformance", false.ToString()) });
            }

            this.WithEnvironment(new[] { ("experimentalFeatures__enabled", "true") })
                .WithEnvironment(new[] { ("experimentalFeatures__nestedEdgeEnabled", "true") })
                .WithEnvironment(new[] { ("experimentalFeatures__mqttBrokerEnabled", "true") })
                .WithEnvironment(new[] { ("RUST_LOG", "debug") });
        }
    }
}
