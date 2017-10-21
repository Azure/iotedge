// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DeploymentConfigInfo
    {
        [JsonConstructor]
        public DeploymentConfigInfo(long version, DeploymentConfig deploymentConfig)
        {
            this.Version = Preconditions.CheckRange(version, -1, nameof(version));
            this.DeploymentConfig = Preconditions.CheckNotNull(deploymentConfig, nameof(deploymentConfig));
        }

        public long Version { get; }

        public DeploymentConfig DeploymentConfig { get; }
    }
}
