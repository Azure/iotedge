// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;

    public class HubModuleConfigBuilder : ModuleConfigBuilder
    {
        const string DefaultImage = "mcr.microsoft.com/azureiotedge-hub:1.0";
        const string HubCreateOptions = "{\"HostConfig\":{\"PortBindings\":{\"8883/tcp\":[{\"HostPort\":\"8883\"}],\"443/tcp\":[{\"HostPort\":\"443\"}],\"5671/tcp\":[{\"HostPort\":\"5671\"}]}}}";

        public HubModuleConfigBuilder(Option<string> image, bool optimizeForPerformance)
            : base(ModuleName.EdgeHub, image.GetOrElse(DefaultImage), Option.Some(HubCreateOptions), true)
        {
            this.WithDesiredProperties(
                new Dictionary<string, object>
                {
                    ["schemaVersion"] = Context.Current.EdgeHubSchemaVersion,
                    ["routes"] = new { route1 = "from /* INTO $upstream" },
                    ["storeAndForwardConfiguration"] = new { timeToLiveSecs = 7200 }
                });

            if (!optimizeForPerformance)
            {
                this.WithEnvironment(new[] { ("OptimizeForPerformance", false.ToString()) });
            }
        }
    }
}
