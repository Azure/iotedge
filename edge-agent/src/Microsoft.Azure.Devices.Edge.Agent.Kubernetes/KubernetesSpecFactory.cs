// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
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
                 if (string.Equals(crdModule.Type,DockerType, StringComparison.OrdinalIgnoreCase))
                 {
                    var dockerConfig = JsonConvert.DeserializeObject<CombinedDockerConfig>(crdModule.Config);
                    var newModule = new KubernetesModule<CombinedDockerConfig>(crdModule, dockerConfig);
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