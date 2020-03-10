// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class EdgeAgentDockerModule : DockerModule, IEdgeAgentModule
    {
        [JsonConstructor]
        public EdgeAgentDockerModule(string type, DockerConfig settings, ImagePullPolicy imagePullPolicy, ConfigurationInfo configuration, IDictionary<string, EnvVal> env, string version = "")
            : base(Core.Constants.EdgeAgentModuleName, version, ModuleStatus.Running, RestartPolicy.Always, settings, imagePullPolicy, Core.Constants.HighestPriority, configuration, env)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // ReSharper disable once NonReadonlyMemberInGetHashCode
                int hashCode = this.Name != null ? this.Name.GetHashCode() : 0;
                // ReSharper restore NonReadonlyMemberInGetHashCode
                hashCode = (hashCode * 397) ^ (this.Version != null ? this.Version.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Type != null ? this.Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Config?.Image != null ? this.Config.Image.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
