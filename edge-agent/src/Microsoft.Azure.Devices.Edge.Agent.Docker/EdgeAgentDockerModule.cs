// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    // using Microsoft.Extensions.Logging;
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

            bool result = string.Equals(this.Name, other.Name) &&
                string.Equals(this.Type, other.Type) &&
                string.Equals(this.Config.Image, other.Config.Image);
            // ILogger log = Logger.Factory.CreateLogger<EdgeAgentDockerModule>();
            // log.LogInformation($">>> Agent Name/Type/Image equal? {result}");
            if (!result)
                return false;

            IDictionary<string, string> labels = other.Config.CreateOptions?.Labels;
            // if (labels != null && this.Config.CreateOptions?.Labels != null)
            // {
            //     log.LogInformation($">>> THIS labels:\n{JsonConvert.SerializeObject(this.Config.CreateOptions?.Labels)}");
            //     log.LogInformation($">>> OTHER labels:\n{JsonConvert.SerializeObject(labels)}\n");
            // }
            // log.LogInformation($">>> Agent container has agent-* labels? {labels != null && labels.ContainsKey("net.azure-devices.edge.create-options") && labels.ContainsKey("net.azure-devices.edge.env")}");
            if (labels == null ||
                !labels.TryGetValue("net.azure-devices.edge.create-options", out string createOptions) ||
                !labels.TryGetValue("net.azure-devices.edge.env", out string env))
                return true;

            // log.LogInformation($">>> DESIRED CREATE OPTIONS:\n{JsonConvert.SerializeObject(this.Config.CreateOptions)}\n>>> ACTUAL CREATE OPTIONS:\n{createOptions}");
            // log.LogInformation($">>> DESIRED ENV:\n{JsonConvert.SerializeObject(this.Env)}\n>>> ACTUAL ENV:\n{env}\n");

            // If the 'net.azure-devices.edge.create-options' and 'net.azure-devices.edge.create-options' labels exist
            // on the other IModule, compare them to this IModule
            string desiredCreateOptions = JsonConvert.SerializeObject(this.Config.CreateOptions);
            string desiredEnv = JsonConvert.SerializeObject(this.Env);
            // log.LogInformation($">>> AGENT CREATE OPTIONS ARE EQUAL?: {desiredCreateOptions == createOptions}");
            // log.LogInformation($">>> AGENT ENVS ARE EQUAL?: {desiredEnv == env}");
            return desiredCreateOptions == createOptions && desiredEnv == env;
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
