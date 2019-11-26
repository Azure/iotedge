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
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.ServiceAccount;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;
    using CoreConstants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    // TODO add unit tests
    public class EdgeDeploymentController : IEdgeDeploymentController
    {
        static readonly string EdgeAgentDeploymentName = KubeUtils.SanitizeLabelValue(CoreConstants.EdgeAgentModuleName);

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

        public async Task<EdgeDeploymentStatus> DeployModulesAsync(ModuleSet desiredModules, ModuleSet currentModules)
        {
            try
            {
                var moduleIdentities = await this.moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desiredModules, currentModules);

                var labels = desiredModules.Modules
                    .ToDictionary(
                        module => module.Key,
                        module => new Dictionary<string, string>
                        {
                            [KubernetesConstants.K8sEdgeModuleLabel] = moduleIdentities[module.Key].DeploymentName(),
                            [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue(this.resourceName.DeviceId),
                            [KubernetesConstants.K8sEdgeHubNameLabel] = KubeUtils.SanitizeLabelValue(this.resourceName.Hostname)
                        });
                var deviceOnlyLabels = new Dictionary<string, string>
                {
                    [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue(this.resourceName.DeviceId),
                    [KubernetesConstants.K8sEdgeHubNameLabel] = KubeUtils.SanitizeLabelValue(this.resourceName.Hostname)
                };

                var desiredServices = desiredModules.Modules
                    .Select(module => this.serviceMapper.CreateService(moduleIdentities[module.Key], (KubernetesModule)module.Value, labels[module.Key]))
                    .FilterMap()
                    .ToList();

                V1ServiceList currentServices = await this.client.ListNamespacedServiceAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
                await this.ManageServices(currentServices, desiredServices);

                var desiredDeployments = desiredModules.Modules
                    .Select(module => this.deploymentMapper.CreateDeployment(moduleIdentities[module.Key], (KubernetesModule)module.Value, labels[module.Key]))
                    .ToList();

                V1DeploymentList currentDeployments = await this.client.ListNamespacedDeploymentAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
                await this.ManageDeployments(currentDeployments, desiredDeployments);

                var desiredPvcs = desiredModules.Modules
                    .Select(module => this.pvcMapper.CreatePersistentVolumeClaims((KubernetesModule)module.Value, deviceOnlyLabels))
                    .FilterMap()
                    .SelectMany(x => x)
                    .Distinct(KubernetesPvcByValueEqualityComparer);

                // Modules may use PVCs created by the user, we get all PVCs and then work on ours.
                V1PersistentVolumeClaimList currentPvcList = await this.client.ListNamespacedPersistentVolumeClaimAsync(this.deviceNamespace);
                await this.ManagePvcs(currentPvcList, desiredPvcs);

                var desiredServiceAccounts = desiredModules.Modules
                    .Select(module => this.serviceAccountMapper.CreateServiceAccount((KubernetesModule)module.Value, moduleIdentities[module.Key], labels[module.Key]))
                    .ToList();

                V1ServiceAccountList currentServiceAccounts = await this.client.ListNamespacedServiceAccountAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
                await this.ManageServiceAccounts(currentServiceAccounts, desiredServiceAccounts);

                return EdgeDeploymentStatus.Success("Successfully deployed");
            }
            catch (HttpOperationException e)
            {
                Events.DeployModulesException(e);
                return EdgeDeploymentStatus.Failure(e);
            }
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
                        return this.client.DeleteNamespacedDeploymentAsync(
                            name,
                            this.deviceNamespace,
                            propagationPolicy: KubernetesConstants.DefaultDeletePropagationPolicy,
                            body: new V1DeleteOptions(propagationPolicy: KubernetesConstants.DefaultDeletePropagationPolicy));
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

            // Remove all PVCs that are not in the desired list, and are labeled (created by Agent)
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

            return desiredSet.Diff(existingSet, KubernetesServiceAccountByValueEqualityComparer);
        }

        static IEqualityComparer<V1ServiceAccount> KubernetesServiceAccountByValueEqualityComparer { get; } = new KubernetesServiceAccountByValueEqualityComparer();

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
                CreateServiceAccount,
                DeleteServiceAccount,
                UpdateServiceAccount,
                DeployModulesException
            }

            internal static void DeleteService(string name)
            {
                Log.LogInformation((int)EventIds.DeleteService, $"Delete service {name}");
            }

            internal static void CreateService(V1Service service)
            {
                Log.LogInformation((int)EventIds.CreateService, $"Create service {service.Metadata.Name}");
            }

            internal static void CreateDeployment(V1Deployment deployment)
            {
                Log.LogInformation((int)EventIds.CreateDeployment, $"Create deployment {deployment.Metadata.Name}");
            }

            internal static void DeleteDeployment(string name)
            {
                Log.LogInformation((int)EventIds.DeleteDeployment, $"Delete deployment {name}");
            }

            internal static void UpdateDeployment(V1Deployment deployment)
            {
                Log.LogInformation((int)EventIds.UpdateDeployment, $"Update deployment {deployment.Metadata.Name}");
            }

            internal static void CreatePvc(V1PersistentVolumeClaim pvc)
            {
                Log.LogInformation((int)EventIds.CreatePvc, $"Create PVC {pvc.Metadata.Name}");
            }

            internal static void DeletePvc(string name)
            {
                Log.LogInformation((int)EventIds.DeletePvc, $"Delete PVC {name}");
            }

            internal static void UpdatePvc(V1PersistentVolumeClaim pvc)
            {
                Log.LogInformation((int)EventIds.UpdatePvc, $"Update PVC {pvc.Metadata.Name}");
            }

            internal static void DeployModulesException(Exception ex)
            {
                Log.LogWarning((int)EventIds.DeployModulesException, ex, "Module deployment failed");
            }

            internal static void InvalidCreationString(string kind, string name)
            {
                Log.LogDebug((int)EventIds.InvalidCreationString, $"Expected a valid '{kind}' creation string in k8s Object '{name}'.");
            }

            internal static void UpdateService(V1Service service)
            {
                Log.LogDebug((int)EventIds.UpdateService, $"Update service object '{service.Metadata.Name}'");
            }

            internal static void CreateServiceAccount(V1ServiceAccount serviceAccount)
            {
                Log.LogDebug((int)EventIds.CreateServiceAccount, $"Create Service Account {serviceAccount.Metadata.Name}");
            }

            internal static void DeleteServiceAccount(string name)
            {
                Log.LogDebug((int)EventIds.DeleteServiceAccount, $"Delete Service Account {name}");
            }

            internal static void UpdateServiceAccount(V1ServiceAccount serviceAccount)
            {
                Log.LogDebug((int)EventIds.UpdateServiceAccount, $"Update Service Account {serviceAccount.Metadata.Name}");
            }
        }
    }
}
