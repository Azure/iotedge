// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Client
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;

    public static class DeploymentKubernetesClientExtensions
    {
        public static async Task AddModuleDeploymentAsync(this KubernetesClient client, string name, IDictionary<string, string> labels, IDictionary<string, string> annotations)
        {
            var deployment = new V1Deployment
            {
                Metadata = new V1ObjectMeta
                {
                    Name = name,
                    NamespaceProperty = client.DeviceNamespace,
                    Labels = labels
                },
                Spec = new V1DeploymentSpec
                {
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = name,
                            Labels = labels,
                            Annotations = annotations
                        },
                        Spec = new V1PodSpec
                        {
                            Containers = new[]
                            {
                                new V1Container
                                {
                                    Image = "busybox:latest",
                                    Name = name,
                                    Command = new[] { "/bin/sh" },
                                    Args = new[] { "-c", "while true; do echo hello; sleep 10;done" }
                                }
                            },
                            ServiceAccountName = name
                        }
                    },
                    Selector = new V1LabelSelector
                    {
                        MatchLabels = labels
                    }
                }
            };

            await client.Kubernetes.CreateNamespacedDeploymentAsync(deployment, client.DeviceNamespace);
        }

        public static async Task ReplaceModuleImageAsync(this KubernetesClient client, string name, string image)
        {
            V1Deployment deployment = await client.Kubernetes.ReadNamespacedDeploymentAsync(name, client.DeviceNamespace);
            deployment.Spec.Template.Spec.Containers[0].Image = image;

            await client.Kubernetes.ReplaceNamespacedDeploymentAsync(deployment, name, client.DeviceNamespace);
        }

        public static async Task DeleteModuleDeploymentAsync(this KubernetesClient client, string name)
        {
            await client.Kubernetes.DeleteNamespacedDeploymentAsync(name, client.DeviceNamespace);
        }
    }
}
