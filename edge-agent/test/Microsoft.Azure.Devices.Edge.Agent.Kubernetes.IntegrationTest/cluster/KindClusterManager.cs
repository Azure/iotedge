// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Cluster
{
    using System;
    using System.Threading.Tasks;
    using CliWrap;
    using k8s;

    public class KindClusterManager : IKubernetesClusterManager
    {
        readonly string name;

        public KindClusterManager(string name)
        {
            this.name = name;
        }

        public async Task Create()
        {
            await BashCommand($@"kind create cluster --name ""{this.name}""")
                .SetStandardOutputCallback(Console.WriteLine)
                .SetStandardErrorCallback(Console.WriteLine)
                .ExecuteAsync();
        }

        public async Task Delete()
        {
            await BashCommand($@"kind delete cluster --name ""{this.name}""")
                .SetStandardOutputCallback(Console.WriteLine)
                .SetStandardErrorCallback(Console.WriteLine)
                .ExecuteAsync();
        }

        public async Task<IKubernetes> GetClient()
        {
            string path = string.Empty;

            await BashCommand($@"kind get kubeconfig-path --name ""{this.name}""")
                .SetStandardOutputCallback(output => path = output)
                .SetStandardErrorCallback(Console.WriteLine)
                .ExecuteAsync();

            return new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile(path));
        }

        static ICli BashCommand(string command) => Cli.Wrap("/bin/bash").SetArguments($@"-c ""{command}""");
    }
}
