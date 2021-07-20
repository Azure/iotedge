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

        public async Task CreateAsync()
        {
            await BashCommand($@"kind create cluster --name ""{this.name}""")
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Console.WriteLine(line)))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Console.WriteLine(line)))
                .ExecuteAsync();
        }

        public async Task DeleteAsync()
        {
            await BashCommand($@"kind delete cluster --name ""{this.name}""")
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Console.WriteLine(line)))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Console.WriteLine(line)))
                .ExecuteAsync();
        }

        public async Task<IKubernetes> GetClientAsync()
        {
            string path = string.Empty;

            await BashCommand($@"kind get kubeconfig-path --name ""{this.name}""")
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line => path = line))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Console.WriteLine(line)))
                .ExecuteAsync();

            return new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile(path));
        }

        static Command BashCommand(string command) => Cli.Wrap("/bin/bash").WithArguments(new[] { "-c", command });
    }
}
