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
        readonly Uri managementUri;

        public CombinedEdgeletConfigProvider(IEnumerable<AuthConfig> authConfigs,
            Uri workloadUri,
            Uri managementUri)
            : base(authConfigs)
        {
            this.workloadUri = Preconditions.CheckNotNull(workloadUri, nameof(workloadUri));
            this.managementUri = Preconditions.CheckNotNull(managementUri, nameof(managementUri));
        }

        static CreateContainerParameters CloneOrCreateParams(CreateContainerParameters createOptions) =>
            createOptions != null
            ? JsonConvert.DeserializeObject<CreateContainerParameters>(JsonConvert.SerializeObject(createOptions))
            : new CreateContainerParameters();

        public override CombinedDockerConfig GetCombinedConfig(IModule module, IRuntimeInfo runtimeInfo)
        {
            CombinedDockerConfig combinedConfig = base.GetCombinedConfig(module, runtimeInfo);

            // if the workload URI is a Unix domain socket then volume mount it into the container
            CreateContainerParameters createOptions = CloneOrCreateParams(combinedConfig.CreateOptions);
            if (this.workloadUri.Scheme == "unix")
            {                
                SetMountOptions(createOptions, this.workloadUri);
            }

            // If Management URI is Unix domain socket, and the module is the EdgeAgent, then mount it ino the container.
            if (this.managementUri.Scheme == "unix"
                && module.Name.Equals(Constants.EdgeAgentModuleName, StringComparison.OrdinalIgnoreCase))
            {
                SetMountOptions(createOptions, this.managementUri);
            }
            return new CombinedDockerConfig(combinedConfig.Image, createOptions, combinedConfig.AuthConfig);
        }

        static void SetMountOptions(CreateContainerParameters createOptions, Uri uri)
        {
            HostConfig hostConfig = createOptions.HostConfig ?? new HostConfig();
            IList<string> binds = hostConfig.Binds ?? new List<string>();
            binds.Add($"{uri.AbsolutePath}:{uri.AbsolutePath}");

            hostConfig.Binds = binds;
            createOptions.HostConfig = hostConfig;
        }
    }
}
