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
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Deployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Pvc;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.ServiceAccount;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    [Kubernetes]
    public class EdgeDeploymentControllerTest : IClassFixture<KubernetesClusterFixture>, IAsyncLifetime
    {
        readonly KubernetesClient client;

        public EdgeDeploymentControllerTest(KubernetesClusterFixture fixture)
        {
            string deviceNamespace = $"device-{Guid.NewGuid()}";
            this.client = new KubernetesClient(deviceNamespace, fixture.Client);
        }

        public async Task InitializeAsync() => await this.client.AddNamespaceAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        private EdgeDeploymentController CreateDeploymentController(string moduleName, string deviceSelector, DummyModuleIdentityLifecycleManager moduleLifeCycleManager)
        {
            var resourceName = new ResourceName("hostname", "deviceid");     
            var kubernetesServiceMapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);
            string proxyImagePullSecretName = null;
            IDictionary<string, bool> experimentalFeatures = null;
            var persistentVolumeName = string.Empty;
            var storageClassName = string.Empty;
            var deploymentMapper = new KubernetesDeploymentMapper(
                this.client.DeviceNamespace,
                "edgehub",
                "proxy",
                Option.Maybe(proxyImagePullSecretName),
                "configPath",
                "config-volume",
                "configMapName",
                "trustBundlePath",
                "trust-bundle-volume",
                "trustBundleConfigMapName",
                PortMapServiceType.ClusterIP,
                persistentVolumeName,
                storageClassName,
                Option.Some<uint>(100),
                "apiVersion",
                new Uri("http://localhost:35001"),
                new Uri("http://localhost:35000"),
                false,
                false,
                experimentalFeatures == null ? new Dictionary<string, bool>() : experimentalFeatures);
            var pvcMapper = new KubernetesPvcMapper(persistentVolumeName, storageClassName, 100);
            var serviceAccountMapper = new KubernetesServiceAccountMapper();
            return new EdgeDeploymentController(
                resourceName,
                deviceSelector,
                this.client.DeviceNamespace,
                this.client.Kubernetes,
                moduleLifeCycleManager,
                kubernetesServiceMapper,
                deploymentMapper,
                pvcMapper,
                serviceAccountMapper);
        }

        private KubernetesModule CreateKubernetesModule(string moduleName)
        {
            var creatOptions = CreatePodParameters.Create();
            KubernetesConfig config = new KubernetesConfig("image", creatOptions, Option.None<AuthConfig>());
            IModule m1 = new DockerModule(moduleName, "v1", ModuleStatus.Running, RestartPolicy.Always, new DockerConfig("test-image:1"), ImagePullPolicy.OnCreate, Core.Constants.DefaultPriority, null, null);
            return new KubernetesModule(m1, config, new KubernetesModuleOwner("v1", "Deployment", "iotedged", "123"));
        }

        [Fact]
        public async Task CheckIfCreateDeploymentIsSuccessful()
        {
            var moduleName = "module-a";
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceid,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager(
                "hostname",
                "gatewayhostname",
                "deviceid",
                moduleName,
                new ConnectionStringCredentials("connectionString"));
            var controller = CreateDeploymentController(moduleName, deviceSelector, moduleLifeCycleManager);
            KubernetesModule km1 = CreateKubernetesModule(moduleName);

            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Empty);

            V1DeploymentList currentDeployments = await this.client.ListDeployments(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccounts(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServices(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaims();
            Assert.Single(currentDeployments.Items, d => d.Metadata.Name == moduleName);
            Assert.Single(currentServiceAccounts.Items, sa => sa.Metadata.Name == moduleName);
            Assert.Empty(currentServices.Items);
            Assert.Empty(currentPvcList.Items);
        }

        [Fact]
        public async Task CheckIfDeleteDeploymentIsSuccessful()
        {
            var moduleName = "module-a";
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceid,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager(
                "hostname",
                "gatewayhostname",
                "deviceid",
                moduleName,
                new ConnectionStringCredentials("connectionString"));
            var controller = CreateDeploymentController(moduleName, deviceSelector, moduleLifeCycleManager);
            KubernetesModule km1 = CreateKubernetesModule(moduleName);

            await this.client.AddModuleDeploymentAsync(moduleName, new Dictionary<string, string> { ["a"] = "b" }, null);
            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Empty, ModuleSet.Create(km1));

            V1DeploymentList currentDeployments = await this.client.ListDeployments(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccounts(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServices(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaims();
            Assert.Empty(currentDeployments.Items);
            Assert.Empty(currentServiceAccounts.Items);
            Assert.Empty(currentServices.Items);
            Assert.Empty(currentPvcList.Items);
        }

        [Fact]
        public async Task CheckIfUpdateDeploymentWithImageUpdateIsSuccessful()
        {
            var moduleName = "module-a";
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceid,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager(
                "hostname",
                "gatewayhostname",
                "deviceid",
                moduleName,
                new ConnectionStringCredentials("connectionString"));
            var controller = CreateDeploymentController(moduleName, deviceSelector, moduleLifeCycleManager);
            KubernetesModule km1 = CreateKubernetesModule(moduleName);
            string newImage = "test-image:2";

            await this.client.AddModuleDeploymentAsync(moduleName, new Dictionary<string, string> { ["a"] = "b" }, null);
            moduleLifeCycleManager.SetModules(moduleName);
            await this.client.ReplaceModuleImageAsync(moduleName, newImage);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Create(km1));

            V1DeploymentList currentDeployments = await this.client.ListDeployments(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccounts(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServices(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaims();
            Assert.Single(currentDeployments.Items, d => d.Spec.Template.Spec.Containers[0].Image == newImage);
            Assert.Single(currentServiceAccounts.Items, sa => sa.Metadata.Name == moduleName);
            Assert.Empty(currentServices.Items);
            Assert.Empty(currentPvcList.Items);
        }

        public class DummyModuleIdentityLifecycleManager : IModuleIdentityLifecycleManager
        {
            readonly string hostName;
            readonly string gatewayHostname;
            readonly string deviceId;
            readonly string moduleId;
            readonly ICredentials credentials;
            private IImmutableDictionary<string, IModuleIdentity> identites = ImmutableDictionary<string, IModuleIdentity>.Empty;
            public DummyModuleIdentityLifecycleManager(string hostName, string gatewayHostname, string deviceId, string moduleId, ICredentials credentials)
            {
                this.hostName = hostName;
                this.gatewayHostname = gatewayHostname;
                this.deviceId = deviceId;
                this.moduleId = moduleId;
                this.credentials = credentials;
            }

            public Task<IImmutableDictionary<string, IModuleIdentity>> GetModuleIdentitiesAsync(ModuleSet desired, ModuleSet current) => Task.FromResult(this.identites);

            IModuleIdentity CreateModuleIdentity() => new ModuleIdentity(hostName, gatewayHostname, deviceId, moduleId, credentials);

            internal void SetModules(params string[] moduleNames) => this.identites = moduleNames
                .Select(name => new { Name = name, ModuleId = this.CreateModuleIdentity() })
                .ToImmutableDictionary(id => id.Name, id => id.ModuleId);
        }
    }
}
