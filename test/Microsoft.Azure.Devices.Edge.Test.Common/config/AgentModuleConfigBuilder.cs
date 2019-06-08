// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public class AgentModuleConfigBuilder : BaseModuleConfigBuilder
    {
        const string DefaultImage = "mcr.microsoft.com/azureiotedge-agent:1.0";

        public AgentModuleConfigBuilder(Option<string> image)
            : base("edgeAgent", image.GetOrElse(DefaultImage), true)
        {
            this.WithDesiredProperties(
                new Dictionary<string, object>
                {
                    ["schemaVersion"] = "1.0",
                    ["runtime"] = new Dictionary<string, object>
                    {
                        ["type"] = "docker",
                        ["settings"] = new Dictionary<string, object>
                        {
                            ["minDockerVersion"] = "v1.25"
                        }
                    }
                });
        }
    }
}
