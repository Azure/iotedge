// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
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
    using DockerEmptyStruct = global::Docker.DotNet.Models.EmptyStruct;

    [Integration]
    [Kubernetes]
    public class EdgeDeploymentControllerTest : IClassFixture<KubernetesClusterFixture>, IAsyncLifetime
    {
        readonly KubernetesClient client;

        static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);

        public EdgeDeploymentControllerTest(KubernetesClusterFixture fixture)
        {
            string deviceNamespace = $"device-{Guid.NewGuid()}";
            this.client = new KubernetesClient(deviceNamespace, fixture.Client);
        }

        public async Task InitializeAsync() => await this.client.AddNamespaceAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task CheckIfCreateDeploymentIsSuccessfulWithNoResources()
        {
            var moduleName = "module-a";
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceid,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager(
                "hostname",
                "gatewayhostname",
                "deviceid",
                moduleName,
                new ConnectionStringCredentials("connectionString"));
            var creatOptions = CreatePodParameters.Create();
            var controller = this.CreateDeploymentController(deviceSelector, moduleLifeCycleManager, string.Empty, string.Empty);
            string imageName = "test-image:1";
            KubernetesModule km1 = this.CreateKubernetesModule(moduleName, creatOptions, imageName);

            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Empty);

            V1DeploymentList currentDeployments = await this.client.ListDeploymentsAsync(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccountsAsync(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServicesAsync(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaimsAsync();
            Assert.Single(currentDeployments.Items, d => d.Metadata.Name == moduleName);
            Assert.Single(currentServiceAccounts.Items, sa => sa.Metadata.Name == moduleName);
            Assert.Empty(currentServices.Items);
            Assert.Empty(currentPvcList.Items);
        }

        [Fact]
        public async Task CheckIfCreateDeploymentNoServiceWithPvcIsSuccessful()
        {
            var moduleName = "module-a";
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceid,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager(
                "hostname",
                "gatewayhostname",
                "deviceid",
                moduleName,
                new ConnectionStringCredentials("connectionString"));
            var hostConfig = new HostConfig
            {
                Mounts = new List<Mount>
                {
                    new Mount
                    {
                        Type = "volume",
                        ReadOnly = true,
                        Source = "pvname",
                        Target = "/tmp/volume"
                    }
                }
            };
            var persistentVolumeName = "pvname";
            var creatOptions = CreatePodParameters.Create(hostConfig: hostConfig);
            var controller = this.CreateDeploymentController(deviceSelector, moduleLifeCycleManager, persistentVolumeName, "storagename");
            string imageName = "test-image:1";
            KubernetesModule km1 = this.CreateKubernetesModule(moduleName, creatOptions, imageName);
            var pvcName = $"{moduleName}-{persistentVolumeName}";
            var tokenSource = new CancellationTokenSource(DefaultTimeout * 3);

            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Empty);
            await this.client.WaitUntilAnyPersistentVolumeClaimAsync(tokenSource.Token);

            V1DeploymentList currentDeployments = await this.client.ListDeploymentsAsync(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccountsAsync(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServicesAsync(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaimsAsync();
            Assert.Single(currentDeployments.Items, d => d.Metadata.Name == moduleName);
            Assert.Single(currentServiceAccounts.Items, sa => sa.Metadata.Name == moduleName);
            Assert.Empty(currentServices.Items);
            Assert.Single(currentPvcList.Items, p => p.Metadata.Name == pvcName);
        }

        [Fact]
        public async Task CheckIfCreateDeploymentWithServiceNoPvcIsSuccessful()
        {
            var moduleName = "module-a";
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceid,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager(
                "hostname",
                "gatewayhostname",
                "deviceid",
                moduleName,
                new ConnectionStringCredentials("connectionString"));
            var exposedPorts = new Dictionary<string, DockerEmptyStruct>
            {
                ["80/tcp"] = default
            };
            var creatOptions = CreatePodParameters.Create(exposedPorts: exposedPorts);
            var controller = this.CreateDeploymentController(deviceSelector, moduleLifeCycleManager, string.Empty, string.Empty);
            string imageName = "test-image:1";
            KubernetesModule km1 = this.CreateKubernetesModule(moduleName, creatOptions, imageName);

            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Empty);

            V1DeploymentList currentDeployments = await this.client.ListDeploymentsAsync(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccountsAsync(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServicesAsync(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaimsAsync();
            Assert.Single(currentDeployments.Items, d => d.Metadata.Name == moduleName);
            Assert.Single(currentServiceAccounts.Items, sa => sa.Metadata.Name == moduleName);
            Assert.Single(currentServices.Items, s => s.Metadata.Name == moduleName);
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
            var exposedPorts = new Dictionary<string, DockerEmptyStruct>
            {
                ["80/tcp"] = default
            };
            var hostConfig = new HostConfig
            {
                Mounts = new List<Mount>
                {
                    new Mount
                    {
                        Type = "volume",
                        ReadOnly = true,
                        Source = "pvname",
                        Target = "/tmp/volume"
                    }
                }
            };
            var creatOptions = CreatePodParameters.Create(exposedPorts: exposedPorts, hostConfig: hostConfig);
            var controller = this.CreateDeploymentController(deviceSelector, moduleLifeCycleManager, "pvname", "storagename");
            string imageName = "test-image:1";
            KubernetesModule km1 = this.CreateKubernetesModule(moduleName, creatOptions, imageName);
            var labels = new Dictionary<string, string>
            {
                ["net.azure-devices.edge.deviceid"] = "deviceid",
                ["net.azure-devices.edge.hub"] = "hostname",
                ["net.azure-devices.edge.module"] = moduleName
            };

            await this.client.AddModuleDeploymentAsync(moduleName, labels, null);
            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Empty, ModuleSet.Create(km1));

            V1DeploymentList currentDeployments = await this.client.ListDeploymentsAsync(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccountsAsync(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServicesAsync(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaimsAsync();
            Assert.Empty(currentDeployments.Items);
            Assert.Empty(currentServiceAccounts.Items);
            Assert.Empty(currentServices.Items);
            Assert.Empty(currentPvcList.Items);
        }

        [Fact]
        public async Task CheckIfDeploymentIsHealthyWithDeploymentDeletion()
        {
            var moduleName = "module-a";
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceid,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager(
                "hostname",
                "gatewayhostname",
                "deviceid",
                moduleName,
                new ConnectionStringCredentials("connectionString"));
            var creatOptions = CreatePodParameters.Create();
            var controller = this.CreateDeploymentController(deviceSelector, moduleLifeCycleManager, string.Empty, string.Empty);
            string imageName = "test-image:1";
            KubernetesModule km1 = this.CreateKubernetesModule(moduleName, creatOptions, imageName);
            var labels = new Dictionary<string, string>
            {
                ["net.azure-devices.edge.deviceid"] = "deviceid",
                ["net.azure-devices.edge.hub"] = "hostname",
                ["net.azure-devices.edge.module"] = moduleName
            };

            await this.client.AddModuleDeploymentAsync(moduleName, labels, null);
            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Create(km1));
            this.client.DeleteDeployment(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Create(km1));

            V1DeploymentList currentDeployments = await this.client.ListDeploymentsAsync(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccountsAsync(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServicesAsync(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaimsAsync();
            Assert.Single(currentDeployments.Items, d => d.Metadata.Name == moduleName);
            Assert.Single(currentServiceAccounts.Items, sa => sa.Metadata.Name == moduleName);
            Assert.Empty(currentServices.Items);
            Assert.Empty(currentPvcList.Items);
        }

        [Fact]
        public async Task CheckIfDeploymentIsHealthyWithPvcDeletion()
        {
            var moduleName = "module-a";
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceid,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager(
                "hostname",
                "gatewayhostname",
                "deviceid",
                moduleName,
                new ConnectionStringCredentials("connectionString"));
            var hostConfig = new HostConfig
            {
                Mounts = new List<Mount>
                {
                    new Mount
                    {
                        Type = "volume",
                        ReadOnly = true,
                        Source = "pvname",
                        Target = "/tmp/volume"
                    }
                }
            };
            var persistentVolumeName = "pvname";
            var creatOptions = CreatePodParameters.Create(hostConfig: hostConfig);
            var controller = this.CreateDeploymentController(deviceSelector, moduleLifeCycleManager, persistentVolumeName, "storagename");
            string imageName = "test-image:1";
            KubernetesModule km1 = this.CreateKubernetesModule(moduleName, creatOptions, imageName);
            var labels = new Dictionary<string, string>
            {
                ["net.azure-devices.edge.deviceid"] = "deviceid",
                ["net.azure-devices.edge.hub"] = "hostname",
                ["net.azure-devices.edge.module"] = moduleName
            };
            var tokenSource = new CancellationTokenSource(DefaultTimeout * 3);
            var pvcName = $"{moduleName}-{persistentVolumeName}";

            await this.client.AddModuleDeploymentAsync(moduleName, labels, null);
            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Create(km1));
            await this.client.WaitUntilAnyPersistentVolumeClaimAsync(tokenSource.Token);
            this.client.DeletePvc(pvcName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Create(km1));

            V1DeploymentList currentDeployments = await this.client.ListDeploymentsAsync(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccountsAsync(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServicesAsync(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaimsAsync();
            Assert.Single(currentDeployments.Items, d => d.Metadata.Name == moduleName);
            Assert.Single(currentServiceAccounts.Items, sa => sa.Metadata.Name == moduleName);
            Assert.Empty(currentServices.Items);
            Assert.Single(currentPvcList.Items, p => p.Metadata.Name == pvcName);
        }

        [Fact]
        public async Task CheckIfDeploymentIsHealthyWithServiceAccountDeletion()
        {
            var moduleName = "module-a";
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceid,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager(
                "hostname",
                "gatewayhostname",
                "deviceid",
                moduleName,
                new ConnectionStringCredentials("connectionString"));
            var creatOptions = CreatePodParameters.Create();
            var controller = this.CreateDeploymentController(deviceSelector, moduleLifeCycleManager, string.Empty, string.Empty);
            string imageName = "test-image:1";
            KubernetesModule km1 = this.CreateKubernetesModule(moduleName, creatOptions, imageName);
            var tokenSource = new CancellationTokenSource(DefaultTimeout);
            var labels = new Dictionary<string, string>
            {
                ["net.azure-devices.edge.deviceid"] = "deviceid",
                ["net.azure-devices.edge.hub"] = "hostname",
                ["net.azure-devices.edge.module"] = moduleName
            };

            await this.client.AddModuleDeploymentAsync(moduleName, labels, null);
            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Create(km1));
            this.client.DeleteServiceAccount(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Create(km1));

            V1DeploymentList currentDeployments = await this.client.ListDeploymentsAsync(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccountsAsync(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServicesAsync(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaimsAsync();
            Assert.Single(currentDeployments.Items, d => d.Metadata.Name == moduleName);
            Assert.Single(currentServiceAccounts.Items, sa => sa.Metadata.Name == moduleName);
            Assert.Empty(currentServices.Items);
            Assert.Empty(currentPvcList.Items);
        }

        [Fact]
        public async Task CheckIfDeploymentIsHealthyWithServiceDeletion()
        {
            var moduleName = "module-a";
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceid,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager(
                "hostname",
                "gatewayhostname",
                "deviceid",
                moduleName,
                new ConnectionStringCredentials("connectionString"));
            var exposedPorts = new Dictionary<string, DockerEmptyStruct>
            {
                ["80/tcp"] = default
            };
            var creatOptions = CreatePodParameters.Create(exposedPorts: exposedPorts);
            var controller = this.CreateDeploymentController(deviceSelector, moduleLifeCycleManager, string.Empty, string.Empty);
            string imageName = "test-image:1";
            KubernetesModule km1 = this.CreateKubernetesModule(moduleName, creatOptions, imageName);
            var labels = new Dictionary<string, string>
            {
                ["net.azure-devices.edge.deviceid"] = "deviceid",
                ["net.azure-devices.edge.hub"] = "hostname",
                ["net.azure-devices.edge.module"] = moduleName
            };

            await this.client.AddModuleDeploymentAsync(moduleName, labels, null);
            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Create(km1));
            this.client.DeleteService(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1), ModuleSet.Create(km1));

            V1DeploymentList currentDeployments = await this.client.ListDeploymentsAsync(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccountsAsync(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServicesAsync(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaimsAsync();
            Assert.Single(currentDeployments.Items, d => d.Metadata.Name == moduleName);
            Assert.Single(currentServiceAccounts.Items, sa => sa.Metadata.Name == moduleName);
            Assert.Single(currentServices.Items, s => s.Metadata.Name == moduleName);
            Assert.Empty(currentPvcList.Items);
        }

        [Fact]
        public async Task CheckIfUpdateDeploymentWithAddedPvcIsSuccessful()
        {
            var moduleName = "module-a";
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceid,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager(
                "hostname",
                "gatewayhostname",
                "deviceid",
                moduleName,
                new ConnectionStringCredentials("connectionString"));
            var creatOptions = CreatePodParameters.Create();
            var persistentVolumeName = "pvname";
            var controller = this.CreateDeploymentController(deviceSelector, moduleLifeCycleManager, persistentVolumeName, "storagename");
            string imageName = "test-image:1";
            KubernetesModule km1 = this.CreateKubernetesModule(moduleName, creatOptions, imageName);
            var hostConfigs = new HostConfig
            {
                Mounts = new List<Mount>
                {
                    new Mount
                    {
                        Type = "volume",
                        ReadOnly = true,
                        Source = "pvname",
                        Target = "/tmp/volume"
                    }
                }
            };
            var updatedCreatOptions = new CreatePodParameters(null, null, hostConfigs, null, null);
            KubernetesModule km1updated = this.CreateKubernetesModule(moduleName, updatedCreatOptions, imageName);
            var labels = new Dictionary<string, string>
            {
                ["net.azure-devices.edge.deviceid"] = "deviceid",
                ["net.azure-devices.edge.hub"] = "hostname",
                ["net.azure-devices.edge.module"] = moduleName
            };
            var pvcName = $"{moduleName}-{persistentVolumeName}";
            var tokenSource = new CancellationTokenSource(DefaultTimeout * 3);

            await this.client.AddModuleDeploymentAsync(moduleName, labels, null);
            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1updated), ModuleSet.Create(km1));
            await this.client.WaitUntilAnyPersistentVolumeClaimAsync(tokenSource.Token);

            V1DeploymentList currentDeployments = await this.client.ListDeploymentsAsync(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccountsAsync(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServicesAsync(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaimsAsync();
            Assert.Single(currentDeployments.Items, d => d.Metadata.Name == moduleName);
            Assert.Single(currentServiceAccounts.Items, sa => sa.Metadata.Name == moduleName);
            Assert.Empty(currentServices.Items);
            Assert.Single(currentPvcList.Items, p => p.Metadata.Name == pvcName);
        }

        [Fact]
        public async Task CheckIfUpdateDeploymentWithAddedServiceIsSuccessful()
        {
            var moduleName = "module-a";
            var deviceSelector = $"{Kubernetes.Constants.K8sEdgeDeviceLabel}=deviceid,{Kubernetes.Constants.K8sEdgeHubNameLabel}=hostname";
            var moduleLifeCycleManager = new DummyModuleIdentityLifecycleManager(
                "hostname",
                "gatewayhostname",
                "deviceid",
                moduleName,
                new ConnectionStringCredentials("connectionString"));
            var exposedPorts = new Dictionary<string, DockerEmptyStruct>
            {
                ["80/tcp"] = default
            };
            var creatOptions = CreatePodParameters.Create();
            var updatedCreatOptions = CreatePodParameters.Create(exposedPorts: exposedPorts);
            var controller = this.CreateDeploymentController(deviceSelector, moduleLifeCycleManager, string.Empty, string.Empty);
            string imageName = "test-image:1";
            KubernetesModule km1 = this.CreateKubernetesModule(moduleName, creatOptions, imageName);
            KubernetesModule km1updated = this.CreateKubernetesModule(moduleName, updatedCreatOptions, imageName);
            var labels = new Dictionary<string, string>
            {
                ["net.azure-devices.edge.deviceid"] = "deviceid",
                ["net.azure-devices.edge.hub"] = "hostname",
                ["net.azure-devices.edge.module"] = moduleName
            };

            await this.client.AddModuleDeploymentAsync(moduleName, labels, null);
            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1updated), ModuleSet.Create(km1));

            V1DeploymentList currentDeployments = await this.client.ListDeploymentsAsync(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccountsAsync(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServicesAsync(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaimsAsync();
            Assert.Single(currentDeployments.Items, d => d.Metadata.Name == moduleName);
            Assert.Single(currentServiceAccounts.Items, sa => sa.Metadata.Name == moduleName);
            Assert.Single(currentServices.Items, s => s.Metadata.Name == moduleName);
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
            var creatOptions = CreatePodParameters.Create();
            var controller = this.CreateDeploymentController(deviceSelector, moduleLifeCycleManager, string.Empty, string.Empty);
            string imageName = "test-image:1";
            KubernetesModule km1 = this.CreateKubernetesModule(moduleName, creatOptions, imageName);
            string newImage = "test-image:2";
            KubernetesModule km1UpdaModule = this.CreateKubernetesModule(moduleName, creatOptions, newImage);
            var labels = new Dictionary<string, string>
            {
                ["net.azure-devices.edge.deviceid"] = "deviceid",
                ["net.azure-devices.edge.hub"] = "hostname",
                ["net.azure-devices.edge.module"] = moduleName
            };

            await this.client.AddModuleDeploymentAsync(moduleName, labels, null);
            moduleLifeCycleManager.SetModules(moduleName);
            await controller.DeployModulesAsync(ModuleSet.Create(km1UpdaModule), ModuleSet.Create(km1));
            await this.client.ReplaceModuleImageAsync(moduleName, newImage);
            await controller.DeployModulesAsync(ModuleSet.Create(km1UpdaModule), ModuleSet.Create(km1));

            V1DeploymentList currentDeployments = await this.client.ListDeploymentsAsync(deviceSelector);
            V1ServiceAccountList currentServiceAccounts = await this.client.ListServiceAccountsAsync(deviceSelector);
            V1ServiceList currentServices = await this.client.ListServicesAsync(deviceSelector);
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListPeristentVolumeClaimsAsync();
            Assert.Single(currentDeployments.Items, d => d.Spec.Template.Spec.Containers[0].Image == newImage);
            Assert.Single(currentServiceAccounts.Items, sa => sa.Metadata.Name == moduleName);
            Assert.Empty(currentServices.Items);
            Assert.Empty(currentPvcList.Items);
        }

        private EdgeDeploymentController CreateDeploymentController(string deviceSelector, IModuleIdentityLifecycleManager moduleLifeCycleManager, string persistentVolumeName, string storageClassName)
        {
            var resourceName = new ResourceName("hostname", "deviceid");
            var kubernetesServiceMapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);
            string proxyImagePullSecretName = null;
            IDictionary<string, bool> experimentalFeatures = null;
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

        private KubernetesModule CreateKubernetesModule(string moduleName, CreatePodParameters createOptions, string imageName)
        {
            KubernetesConfig config = new KubernetesConfig("image", createOptions, Option.None<AuthConfig>());
            IModule m1 = new DockerModule(moduleName, "v1", ModuleStatus.Running, RestartPolicy.Always, new DockerConfig(imageName), ImagePullPolicy.OnCreate, Core.Constants.DefaultPriority, null, null);
            return new KubernetesModule(m1, config, new KubernetesModuleOwner("v1", "Deployment", "iotedged", "123"));
        }
    }
}
