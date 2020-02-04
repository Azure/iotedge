// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class AgentModuleConfigBuilder : BaseModuleConfigBuilder
    {
        const string DefaultImage = "mcr.microsoft.com/azureiotedge-agent:1.0";

        public AgentModuleConfigBuilder(Option<string> image)
            : base("$edgeAgent", image.GetOrElse(DefaultImage))
        {
        }
    }
}
