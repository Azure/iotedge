// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Cluster
{
    using System.Threading.Tasks;
    using CliWrap;
    using k8s;

    public class KindKubernetesClientProvider : IKubernetesClientProvider
    {
        readonly string name;

        public KindKubernetesClientProvider(string name)
        {
            this.name = name;
        }

        public async Task<IKubernetes> GetClient()
        {
            string path = string.Empty;

            await Cli.Wrap("kind")
                .SetArguments($@"get kubeconfig-path --name ""{this.name}""")
                .SetStandardOutputCallback(output => path = output)
                .ExecuteAsync();

            return new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile(path));
        }
    }
}
