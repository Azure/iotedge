// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Deployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Pvc;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.ServiceAccount;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Client;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    [Kubernetes]
    public class EdgeDeploymentControllerTest : IClassFixture<KubernetesClusterFixture>, IAsyncLifetime
    {
        readonly KubernetesClient client;

        public async Task InitializeAsync()
        {
            await this.client.AddNamespaceAsync();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact]
        public async void CheckIfCreateDeploymentIsSuccessful()
        {
            var resourceName = new ResourceName("hostname", "deviceId");
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceId,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            // var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager("module-a");
            var kubernetesServiceMapper = new KubernetesServiceMapper(0);
            // var deploymentMapper = new KubernetesDeploymentMapper();
            // var pvcMapper = new KubernetesPvcMapper("pvc1", "sc1", 100);
            // var serviceAccountMapper = new KubernetesServiceAccountMapper();
            // var controller = new EdgeDeploymentController(resourceName, deviceSelector, client.DeviceNamespace, this.client.Kubernetes, moduleLifeCycleManager, kubernetesServiceMapper, deploymentMapper, pvcMapper, serviceAccountMapper);
            // var module = new KubernetesModule();

            // var status = await controller.DeployModulesAsync(ModuleSet.Create(module), ModuleSet.Empty);

            // List<V1Deployment> deployments = await this.client.ListDeployments();
            // Assert.Single(deployments, d => d.Metadata.Name == module.Name);
            // this.client.GetAllResourcesForModule();
            // todo using this.client get list of deployments and check there is deployment with the module.Name
            // todo get list of service accounts and check SA with module.Name
            // todo check no services except of iotedged
            // check no PVC

        }
        /*public class DummyModuleIdentityLifecycleManager : IModuleIdentityLifecycleManager
        {
            readonly string moduleName;
            public DummyModuleIdentityLifecycleManager(string moduleName)
            {
                this.moduleName = moduleName;
            }
            public ModuleIdentity CreateModuleIdentity() => new ModuleIdentity("hostname", "gatewayhostname", "deviceId", "moduleId", CredentialType.None);

            public Task<IImmutableDictionary<string, IModuleIdentity>> GetModuleIdentitiesAsync(ModuleSet desired, ModuleSet current) => this.moduleName.Select(this.CreateModuleIdentity()).ToImmutableDictionary(id => id.Name);

        }*/
    }
}

