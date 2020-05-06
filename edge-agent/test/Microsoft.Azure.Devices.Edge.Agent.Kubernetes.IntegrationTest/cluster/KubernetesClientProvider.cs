// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Cluster
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using k8s;
    using Microsoft.Azure.Devices.Edge.Util;

    public class KubernetesClientProvider : IKubernetesClientProvider
    {
        public Task<IKubernetes> GetClientAsync()
        {
            // load the k8s config from KUBECONFIG or $HOME/.kube/config or in-cluster if its available
            KubernetesClientConfiguration kubeConfig = Option.Maybe(Environment.GetEnvironmentVariable("KUBECONFIG"))
                .Else(() => Option.Maybe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kube", "config")))
                .Filter(File.Exists)
                .Map(path => KubernetesClientConfiguration.BuildConfigFromConfigFile(path))
                .GetOrElse(KubernetesClientConfiguration.InClusterConfig);

            IKubernetes client = new Kubernetes(kubeConfig);
            return Task.FromResult(client);
        }
    }
}
