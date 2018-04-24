// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DeploymentConfigInfo
    {
        public static DeploymentConfigInfo Empty = new DeploymentConfigInfo(-1, DeploymentConfig.Empty);

        [JsonConstructor]
        public DeploymentConfigInfo(long version, DeploymentConfig deploymentConfig)
        {
            this.Version = Preconditions.CheckRange(version, -1, nameof(version));
            this.DeploymentConfig = Preconditions.CheckNotNull(deploymentConfig, nameof(deploymentConfig));
            this.Exception = Option.None<Exception>();
        }

        public DeploymentConfigInfo(long version, Exception ex)
        {
            this.Version = Preconditions.CheckRange(version, -1, nameof(version));
            this.Exception = Option.Some(Preconditions.CheckNotNull(ex, nameof(ex)));
            this.DeploymentConfig = DeploymentConfig.Empty;
        }

        [JsonProperty("version")]
        public long Version { get; }

        [JsonProperty("deploymentConfig")]
        public DeploymentConfig DeploymentConfig { get; }

        [JsonIgnore]
        public Option<Exception> Exception { get; }
    }
}
