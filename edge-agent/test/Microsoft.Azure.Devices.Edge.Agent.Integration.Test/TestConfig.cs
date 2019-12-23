// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Integration.Test
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Newtonsoft.Json;

    public class TestConfig
    {
        [JsonConstructor]
        public TestConfig(DeploymentConfig deploymentConfig, ModuleSet runtimeInfo, Validator validator)
        {
            this.DeploymentConfig = deploymentConfig;
            this.RuntimeInfo = runtimeInfo;
            this.Validator = validator;
        }

        [JsonProperty(PropertyName = "deploymentConfig")]
        public DeploymentConfig DeploymentConfig { get; set; }

        [JsonProperty(PropertyName = "runtimeInfo")]
        public ModuleSet RuntimeInfo { get; set; }

        [JsonProperty(PropertyName = "validator")]
        public Validator Validator { get; set; }
    }
}
