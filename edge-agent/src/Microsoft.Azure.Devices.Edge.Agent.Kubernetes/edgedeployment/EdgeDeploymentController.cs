// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Deployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Diff;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Pvc;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;
    using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;
    using CoreConstants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    // TODO add unit tests
    public class EdgeDeploymentController : IEdgeDeploymentController
    {
        readonly IKubernetes client;

        readonly ResourceName resourceName;
        readonly string deploymentSelector;
        readonly string deviceNamespace;
        readonly IModuleIdentityLifecycleManager moduleIdentityLifecycleManager;
        readonly IKubernetesServiceMapper serviceMapper;
        readonly IKubernetesDeploymentMapper deploymentMapper;
        readonly IKubernetesPvcMapper pvcMapper;
        readonly IKubernetesServiceAccountMapper serviceAccountMapper;

        public EdgeDeploymentController(
            ResourceName resourceName,
            string deploymentSelector,
            string deviceNamespace,
            IKubernetes client,
            IModuleIdentityLifecycleManager moduleIdentityLifecycleManager,
            IKubernetesServiceMapper serviceMapper,
            IKubernetesDeploymentMapper deploymentMapper,
            IKubernetesPvcMapper pvcMapper,
            IKubernetesServiceAccountMapper serviceAccountMapper)
        {
            this.resourceName = resourceName;
            this.deploymentSelector = deploymentSelector;
            this.deviceNamespace = deviceNamespace;
            this.moduleIdentityLifecycleManager = moduleIdentityLifecycleManager;
            this.client = client;
            this.serviceMapper = serviceMapper;
            this.deploymentMapper = deploymentMapper;
            this.pvcMapper = pvcMapper;
            this.serviceAccountMapper = serviceAccountMapper;
        }

        public async Task<ModuleSet> DeployModulesAsync(IReadOnlyList<KubernetesModule> modules, ModuleSet currentModules)
        {
            var desiredModules = ModuleSet.Create(modules.ToArray());
            var moduleIdentities = await this.moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desiredModules, currentModules);

            var labels = modules
                .ToDictionary(
                    module => module.Name,
                    module => new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeModuleLabel] = moduleIdentities[module.Name].DeploymentName(),
                        [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue(this.resourceName.DeviceId),
                        [KubernetesConstants.K8sEdgeHubNameLabel] = KubeUtils.SanitizeLabelValue(this.resourceName.Hostname)
                    });
            var deviceOnlyLabels = new Dictionary<string, string>
            {
                [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue(this.resourceName.DeviceId),
                [KubernetesConstants.K8sEdgeHubNameLabel] = KubeUtils.SanitizeLabelValue(this.resourceName.Hostname)
            };

            var desiredServices = modules
                .Select(module => this.serviceMapper.CreateService(moduleIdentities[module.Name], module, labels[module.Name]))
                .FilterMap()
                .ToList();

            V1ServiceList currentServices = await this.client.ListNamespacedServiceAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
            await this.ManageServices(currentServices, desiredServices);

            var desiredDeployments = modules
                .Select(module => this.deploymentMapper.CreateDeployment(moduleIdentities[module.Name], module, labels[module.Name]))
                .ToList();

            V1DeploymentList currentDeployments = await this.client.ListNamespacedDeploymentAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
            await this.ManageDeployments(currentDeployments, desiredDeployments);

            var desiredPvcs = modules
                .Select(module => this.pvcMapper.CreatePersistentVolumeClaims(module, deviceOnlyLabels))
                .FilterMap()
                .SelectMany(x => x)
                .Distinct(KubernetesPvcByValueEqualityComparer);

            // Modules may use PVCs created by the user, we get all PVCs and then work on ours.
            V1PersistentVolumeClaimList currentPvcList = await this.client.ListNamespacedPersistentVolumeClaimAsync(this.deviceNamespace);
            await this.ManagePvcs(currentPvcList, desiredPvcs);

            var desiredServiceAccounts = modules
                .Select(module => this.serviceAccountMapper.CreateServiceAccount(moduleIdentities[module.Name], labels[module.Name]))
                .ToList();

            V1ServiceAccountList currentServiceAccounts = await this.client.ListNamespacedServiceAccountAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
            await this.ManageServiceAccounts(currentServiceAccounts, desiredServiceAccounts);

            return desiredModules;
        }

        async Task ManageServices(V1ServiceList existing, IEnumerable<V1Service> desired)
        {
            // find difference between desired and existing services
            var diff = FindServiceDiff(desired, existing.Items);

            // Update only those services if configurations have not matched
            var updatingTask = diff.Updated
                .Select(
                    update =>
                    {
                        Events.UpdateService(update.To);

                        this.serviceMapper.UpdateService(update.To, update.From);
                        return this.client.ReplaceNamespacedServiceAsync(update.To, update.To.Metadata.Name, this.deviceNamespace);
                    });
            await Task.WhenAll(updatingTask);

            // Delete all existing services that are not in desired list
            var removingTasks = diff.Removed
                .Select(
                    name =>
                    {
                        Events.DeleteService(name);
                        return this.client.DeleteNamespacedServiceAsync(name, this.deviceNamespace);
                    });
            await Task.WhenAll(removingTasks);

            // Create new desired services
            var addingTasks = diff.Added
                .Select(
                    service =>
                    {
                        Events.CreateService(service);
                        return this.client.CreateNamespacedServiceAsync(service, this.deviceNamespace);
                    });
            await Task.WhenAll(addingTasks);
        }

        static Diff<V1Service> FindServiceDiff(IEnumerable<V1Service> desired, IEnumerable<V1Service> existing)
        {
            var desiredSet = new Set<V1Service>(desired.ToDictionary(service => service.Metadata.Name));
            var existingSet = new Set<V1Service>(existing.ToDictionary(service => service.Metadata.Name));

            return desiredSet.Diff(existingSet, ServiceByCreationStringEqualityComparer);
        }

        static IEqualityComparer<V1Service> ServiceByCreationStringEqualityComparer { get; } = new KubernetesServiceByCreationStringEqualityComparer();

        async Task ManageDeployments(V1DeploymentList existing, IEnumerable<V1Deployment> desired)
        {
            // find difference between desired and existing deployments
            var diff = FindDeploymentDiff(desired, existing.Items);

            var updatingTask = diff.Updated
                .Select(
                    update =>
                    {
                        Events.UpdateDeployment(update.To);

                        this.deploymentMapper.UpdateDeployment(update.To, update.From);
                        return this.client.ReplaceNamespacedDeploymentAsync(update.To, update.To.Metadata.Name, this.deviceNamespace);
                    });
            await Task.WhenAll(updatingTask);

            // Delete all existing deployments that are not in desired list
            var removingTasks = diff.Removed
                .Select(
                    name =>
                    {
                        Events.DeleteDeployment(name);
                        return this.client.DeleteNamespacedDeployment1Async(
                            name,
                            this.deviceNamespace,
                            propagationPolicy: "Foreground",
                            body: new V1DeleteOptions(propagationPolicy: "Foreground"));
                    });
            await Task.WhenAll(removingTasks);

            // Add all new deployments from desired list
            var addingTasks = diff.Added
                .Select(
                    deployment =>
                    {
                        Events.CreateDeployment(deployment);
                        return this.client.CreateNamespacedDeploymentAsync(deployment, this.deviceNamespace);
                    });
            await Task.WhenAll(addingTasks);
        }

        static Diff<V1Deployment> FindDeploymentDiff(IEnumerable<V1Deployment> desired, IEnumerable<V1Deployment> existing)
        {
            var desiredSet = new Set<V1Deployment>(desired.ToDictionary(deployment => deployment.Metadata.Name));
            var existingSet = new Set<V1Deployment>(existing.ToDictionary(deployment => deployment.Metadata.Name));

            return desiredSet.Diff(existingSet, DeploymentByCreationStringEqualityComparer);
        }

        static IEqualityComparer<V1Deployment> DeploymentByCreationStringEqualityComparer { get; } = new KubernetesDeploymentByCreationStringEqualityComparer();

        async Task ManagePvcs(V1PersistentVolumeClaimList existing, IEnumerable<V1PersistentVolumeClaim> desired)
        {
            // Find the difference between desired and existing PVCs
            var diff = this.FindPvcDiff(desired, existing.Items);

            // Update all PVCs that are in both lists, and are labeled (created by Agent)
            var updatingTask = diff.Updated
                .Select(
                    update =>
                    {
                        Events.UpdatePvc(update.To);
                        this.pvcMapper.UpdatePersistentVolumeClaim(update.To, update.From);
                        return this.client.ReplaceNamespacedPersistentVolumeClaimAsync(update.To, update.To.Metadata.Name, this.deviceNamespace);
                    });
            await Task.WhenAll(updatingTask);

            try
            {
                // Remove all PVCs that are not in the desired list, and are labled (created by Agent)
                var removingTasks = diff.Removed
                    .Select(
                        name =>
                        {
                            Events.DeletePvc(name);
                            return this.client.DeleteNamespacedPersistentVolumeClaimAsync(name, this.deviceNamespace);
                        });
                await Task.WhenAll(removingTasks);

                // Create all new desired PVCs.
                var addingTasks = diff.Added
                    .Select(
                        pvc =>
                        {
                            Events.CreatePvc(pvc);
                            return this.client.CreateNamespacedPersistentVolumeClaimAsync(pvc, this.deviceNamespace);
                        });
                await Task.WhenAll(addingTasks);
            }
            catch (HttpOperationException ex)
            {
                // Some PVCs may not allow updates, depending on the PV, the reasons for update,
                // or the k8s server version.
                // Also some PVCs may not allow deletion immediately (while pod still exists),
                // or may require user intervention, like deleting the PV created under a storage class.
                // Our best option is to log it and wait for a resolution.
                Events.PvcException(ex);
            }
        }

        Diff<V1PersistentVolumeClaim> FindPvcDiff(
                IEnumerable<V1PersistentVolumeClaim> desired,
                IEnumerable<V1PersistentVolumeClaim> existing)
        {
            var existingDict = existing.ToDictionary(pvc => pvc.Metadata.Name);
            var desiredSet = new Set<V1PersistentVolumeClaim>(desired.ToDictionary(pvc => pvc.Metadata.Name));
            var existingSet = new Set<V1PersistentVolumeClaim>(existingDict);
            var fullDiff = desiredSet.Diff(existingSet, KubernetesPvcByValueEqualityComparer);
            // In fullDiff:
            // Added are `desired` PVCs which are named differently that all existing PVCs.
            //  - these are all new,
            // Removed are all PVCs which are in `existing` and not in `desired`
            //  - some of these names may be PVCs created by the user, we don't want to delete them.
            // Updated are all PVCs which differ between `existing` and `desired`
            //  - some of the "From" PVCs were created by the user, we shouldn't update these.
            // Filter Removed and Updated to only select ones created by controller.
            return new Diff<V1PersistentVolumeClaim>(
                    fullDiff.Added,
                    fullDiff.Removed.Where(name => this.IsCreatedByController(existingDict[name])),
                    fullDiff.Updated.Where(update => this.IsCreatedByController(update.From)));
        }

        bool IsCreatedByController(V1PersistentVolumeClaim claim)
        {
            var labels = claim.Metadata?.Labels;
            if (labels == null)
            {
                return false;
            }

            if (!labels.ContainsKey(KubernetesConstants.K8sEdgeDeviceLabel) || !labels.ContainsKey(KubernetesConstants.K8sEdgeHubNameLabel))
            {
                return false;
            }

            return labels[KubernetesConstants.K8sEdgeDeviceLabel] == KubeUtils.SanitizeLabelValue(this.resourceName.DeviceId) &&
                    labels[KubernetesConstants.K8sEdgeHubNameLabel] == KubeUtils.SanitizeLabelValue(this.resourceName.Hostname);
        }

        static IEqualityComparer<V1PersistentVolumeClaim> KubernetesPvcByValueEqualityComparer { get; } = new KubernetesPvcByValueEqualityComparer();

        async Task ManageServiceAccounts(V1ServiceAccountList existing, IReadOnlyCollection<V1ServiceAccount> desired)
        {
            // find difference between desired and existing service accounts
            var diff = FindServiceAccountDiff(desired, existing.Items);

            // Update all service accounts that are in both lists
            var updatingTasks = diff.Updated
                .Select(
                    update =>
                    {
                        Events.UpdateServiceAccount(update.To);

                        this.serviceAccountMapper.UpdateServiceAccount(update.To, update.From);
                        return this.client.ReplaceNamespacedServiceAccountAsync(update.To, update.To.Metadata.Name, this.deviceNamespace);
                    });
            await Task.WhenAll(updatingTasks);

            // Delete only those existing service accounts that are not in in desired list of common names
            var removingTasks = diff.Removed
                .Select(
                    name =>
                    {
                        Events.DeleteServiceAccount(name);
                        return this.client.DeleteNamespacedServiceAccountAsync(name, this.deviceNamespace);
                    });
            await Task.WhenAll(removingTasks);

            // Add only those desired service account that are not in the list of common names
            var addingTasks = diff.Added
                .Select(
                    account =>
                    {
                        Events.CreateServiceAccount(account);
                        return this.client.CreateNamespacedServiceAccountAsync(account, this.deviceNamespace);
                    });
            await Task.WhenAll(addingTasks);
        }

        static Diff<V1ServiceAccount> FindServiceAccountDiff(IEnumerable<V1ServiceAccount> desired, IEnumerable<V1ServiceAccount> existing)
        {
            var desiredSet = new Set<V1ServiceAccount>(desired.ToDictionary(serviceAccount => serviceAccount.Metadata.Name));
            var existingSet = new Set<V1ServiceAccount>(existing.ToDictionary(serviceAccount => serviceAccount.Metadata.Name));

            return desiredSet.Diff(existingSet);
        }

        public async Task PurgeModulesAsync()
        {
            // Delete all services for current edge deployment
            V1ServiceList services = await this.client.ListNamespacedServiceAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
            var serviceTasks = services.Items
                .Select(service => this.client.DeleteNamespacedServiceAsync(service.Metadata.Name, this.deviceNamespace, new V1DeleteOptions()));
            await Task.WhenAll(serviceTasks);

            // Delete all deployments for current edge deployment
            V1DeploymentList deployments = await this.client.ListNamespacedDeploymentAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
            var deploymentTasks = deployments.Items
                .Select(
                    deployment => this.client.DeleteNamespacedDeployment1Async(
                        deployment.Metadata.Name,
                        this.deviceNamespace,
                        new V1DeleteOptions(propagationPolicy: "Foreground"),
                        propagationPolicy: "Foreground"));
            await Task.WhenAll(deploymentTasks);

            V1PersistentVolumeClaimList pvcs = await this.client.ListNamespacedPersistentVolumeClaimAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
            var pvcTasks = pvcs.Items
                .Select(pvc => this.client.DeleteNamespacedPersistentVolumeClaimAsync(pvc.Metadata.Name, this.deviceNamespace, new V1DeleteOptions()));
            await Task.WhenAll(pvcTasks);

            // Delete the service account for all deployments
            V1ServiceAccountList serviceAccounts = await this.client.ListNamespacedServiceAccountAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
            var serviceAccountTasks = serviceAccounts.Items
                .Select(service => this.client.DeleteNamespacedServiceAsync(service.Metadata.Name, this.deviceNamespace, new V1DeleteOptions()));
            await Task.WhenAll(serviceAccountTasks);
        }

        static class Events
        {
            const int IdStart = KubernetesEventIds.EdgeDeploymentController;
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeDeploymentController>();

            enum EventIds
            {
                InvalidCreationString = IdStart,
                CreateService,
                DeleteService,
                UpdateService,
                CreateDeployment,
                DeleteDeployment,
                UpdateDeployment,
                CreatePvc,
                DeletePvc,
                UpdatePvc,
                PvcException,
                CreateServiceAccount,
                DeleteServiceAccount,
                UpdateServiceAccount
            }

            public static void DeleteService(string name)
            {
                Log.LogInformation((int)EventIds.DeleteService, $"Delete service {name}");
            }

            public static void CreateService(V1Service service)
            {
                Log.LogInformation((int)EventIds.CreateService, $"Create service {service.Metadata.Name}");
            }

            public static void CreateDeployment(V1Deployment deployment)
            {
                Log.LogInformation((int)EventIds.CreateDeployment, $"Create deployment {deployment.Metadata.Name}");
            }

            public static void DeleteDeployment(string name)
            {
                Log.LogInformation((int)EventIds.DeleteDeployment, $"Delete deployment {name}");
            }

            public static void UpdateDeployment(V1Deployment deployment)
            {
                Log.LogInformation((int)EventIds.UpdateDeployment, $"Update deployment {deployment.Metadata.Name}");
            }

            public static void CreatePvc(V1PersistentVolumeClaim pvc)
            {
                Log.LogInformation((int)EventIds.CreatePvc, $"Create PVC {pvc.Metadata.Name}");
            }

            public static void DeletePvc(string name)
            {
                Log.LogInformation((int)EventIds.DeletePvc, $"Delete PVC {name}");
            }

            public static void UpdatePvc(V1PersistentVolumeClaim pvc)
            {
                Log.LogInformation((int)EventIds.UpdatePvc, $"Update PVC {pvc.Metadata.Name}");
            }

            public static void PvcException(Exception ex)
            {
                Log.LogWarning((int)EventIds.PvcException, ex, "PVC update or deletion failed. This may reconcile over time or require operator intervention.");
            }

            public static void InvalidCreationString(string kind, string name)
            {
                Log.LogDebug((int)EventIds.InvalidCreationString, $"Expected a valid '{kind}' creation string in k8s Object '{name}'.");
            }

            public static void UpdateService(V1Service service)
            {
                Log.LogDebug((int)EventIds.UpdateService, $"Update service object '{service.Metadata.Name}'");
            }

            public static void CreateServiceAccount(V1ServiceAccount serviceAccount)
            {
                Log.LogDebug((int)EventIds.CreateServiceAccount, $"Create Service Account {serviceAccount.Metadata.Name}");
            }

            public static void DeleteServiceAccount(string name)
            {
                Log.LogDebug((int)EventIds.DeleteServiceAccount, $"Delete Service Account {name}");
            }

            public static void UpdateServiceAccount(V1ServiceAccount serviceAccount)
            {
                Log.LogDebug((int)EventIds.UpdateServiceAccount, $"Update Service Account {serviceAccount.Metadata.Name}");
            }
        }
    }
}
