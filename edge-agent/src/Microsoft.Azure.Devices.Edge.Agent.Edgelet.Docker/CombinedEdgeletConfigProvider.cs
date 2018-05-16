// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Docker
{
    using System;
    using System.Collections.Generic;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class CombinedEdgeletConfigProvider : CombinedDockerConfigProvider
    {
        readonly Uri workloadUri;

        public CombinedEdgeletConfigProvider(IEnumerable<AuthConfig> authConfigs, Uri workloadUri)
            : base(authConfigs)
        {
            this.workloadUri = Preconditions.CheckNotNull(workloadUri, nameof(workloadUri));
        }

        static CreateContainerParameters CloneOrCreateParams(CreateContainerParameters createOptions)
        {
            createOptions = createOptions != null ?
                JsonConvert.DeserializeObject<CreateContainerParameters>(JsonConvert.SerializeObject(createOptions)) : new CreateContainerParameters();

            return createOptions;
        }

        public override CombinedDockerConfig GetCombinedConfig(IModule module, IRuntimeInfo runtimeInfo)
        {
            CombinedDockerConfig combinedConfig = base.GetCombinedConfig(module, runtimeInfo);

            // if the workload URI is a Unix domain socket then volume mount it into the container
            if (this.workloadUri.Scheme != "unix")
                return combinedConfig;

            CreateContainerParameters createOptions = CloneOrCreateParams(combinedConfig.CreateOptions);
            HostConfig hostConfig = createOptions.HostConfig ?? new HostConfig();
            IList<string> binds = hostConfig.Binds ?? new List<string>();
            binds.Add($"{this.workloadUri.AbsolutePath}:{this.workloadUri.AbsolutePath}");

            hostConfig.Binds = binds;
            createOptions.HostConfig = hostConfig;

            combinedConfig = new CombinedDockerConfig(combinedConfig.Image, createOptions, combinedConfig.AuthConfig);

            return combinedConfig;
        }
    }
}
