// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class AgentModuleConfigBuilder : BaseModuleConfigBuilder
    {
        const string DefaultImage = "$upstream:443/microsoft/azureiotedge-agent:20201214.4-linux-amd64";

        public AgentModuleConfigBuilder(Option<string> image)
            : base(ModuleName.EdgeAgent, image.GetOrElse(DefaultImage))
        {
        }
    }
}
