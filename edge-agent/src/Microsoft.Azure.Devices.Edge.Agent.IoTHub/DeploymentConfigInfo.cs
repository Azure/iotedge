// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class DeploymentConfigInfo
    {
        public DeploymentConfigInfo(long desiredPropertiesVersion, DeploymentConfig deploymentConfig)
        {
            this.DesiredPropertiesVersion = Preconditions.CheckRange(desiredPropertiesVersion, 0, nameof(desiredPropertiesVersion));
            this.DeploymentConfig = Preconditions.CheckNotNull(deploymentConfig, nameof(deploymentConfig));
        }

        public long DesiredPropertiesVersion { get; }

        public DeploymentConfig DeploymentConfig { get; }
    }
}
