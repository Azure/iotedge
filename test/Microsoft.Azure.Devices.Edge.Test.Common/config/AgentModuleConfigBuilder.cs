// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public class AgentModuleConfigBuilder : BaseModuleConfigBuilder
    {
        const string DefaultImage = "mcr.microsoft.com/azureiotedge-agent:1.0";

        public AgentModuleConfigBuilder(Option<string> image)
            : base(ModuleName.EdgeAgent, image.GetOrElse(DefaultImage))
        {
            // BEARWASHERE -- Use EdgeAgent schema 1.1 for startupOrder
            this.WithDesiredProperties(
                new Dictionary<string, object>
                {
                    ["schemaVersion"] = "1.1",
                    ["systemModules"] = new Dictionary<string, object>
                    {
                        ["edgeHub"] = new { startupOrder = 0 }
                    }
                });
        }
    }
}
