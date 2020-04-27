// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Collections.Generic;
    using System.Linq;
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

        public override bool Equals(IModule<DockerConfig> other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            if (!string.Equals(this.Name, other.Name) ||
                !string.Equals(this.Type, other.Type) ||
                !string.Equals(this.Config.Image, other.Config.Image))
                return false;

            IDictionary<string, string> labels = other.Config.CreateOptions?.Labels ?? new Dictionary<string, string>();
            if (!labels.TryGetValue(Core.Constants.Labels.CreateOptions, out string createOptions) ||
                !labels.TryGetValue(Core.Constants.Labels.Env, out string env))
                return true;

            // If the 'net.azure-devices.edge.create-options' and 'net.azure-devices.edge.env' labels exist
            // on the other IModule, compare them to this IModule
            if (JsonConvert.SerializeObject(this.Config.CreateOptions) != createOptions)
                return false;

            var desiredEnv = JsonConvert.DeserializeObject<IDictionary<string, EnvVal>>(env);
            return desiredEnv.Count == this.Env.Count &&
                !desiredEnv.Keys.Except(this.Env.Keys).Any() &&
                !this.Env.Keys.Except(desiredEnv.Keys).Any() &&
                desiredEnv.All(v => this.Env.TryGetValue(v.Key, out EnvVal val) && val.Equals(v.Value));
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
