// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class KubernetesSpecFactory<CombinedDockerConfig> : IKubernetesSpecFactory<CombinedDockerConfig>
    {
        const string DockerType = "docker";

        public IList<KubernetesModule<CombinedDockerConfig>> GetSpec(IList<KubernetesModule<string>> crdSpec)
        {
             var newList = new List<KubernetesModule<CombinedDockerConfig>>();
             foreach (var crdModule in crdSpec)
             {
                 if (crdModule.Type == DockerType)
                 {
                    var dockerConfig = JsonConvert.DeserializeObject<CombinedDockerConfig>(crdModule.Config);
                    var newModule = new KubernetesModule<CombinedDockerConfig>(crdModule) {
                        Config = dockerConfig
                    };
                    newList.Add(newModule);
                 }
                 else
                 {
                     throw new InvalidModuleException($"Expected CombinedDockerConfig, received {crdModule.Type}");
                 }
             }
             return newList;
        }
    }
}