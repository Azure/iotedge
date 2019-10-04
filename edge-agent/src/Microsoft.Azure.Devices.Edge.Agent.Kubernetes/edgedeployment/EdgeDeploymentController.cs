// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Deployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Diff;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
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
        readonly IKubernetesServiceAccountMapper serviceAccountMapper;

        public EdgeDeploymentController(
            ResourceName resourceName,
            string deploymentSelector,
            string deviceNamespace,
            IKubernetes client,
            IModuleIdentityLifecycleManager moduleIdentityLifecycleManager,
            IKubernetesServiceMapper serviceMapper,
            IKubernetesDeploymentMapper deploymentMapper,
            IKubernetesServiceAccountMapper serviceAccountMapper)
        {
            this.resourceName = resourceName;
            this.deploymentSelector = deploymentSelector;
            this.deviceNamespace = deviceNamespace;
            this.moduleIdentityLifecycleManager = moduleIdentityLifecycleManager;
            this.client = client;
            this.serviceMapper = serviceMapper;
            this.deploymentMapper = deploymentMapper;
            this.serviceAccountMapper = serviceAccountMapper;
        }

        public async Task<ModuleSet> DeployModulesAsync(IList<KubernetesModule> modules, ModuleSet currentModules)
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

            var desiredServices = modules
                .Select(module => this.serviceMapper.CreateService(moduleIdentities[module.Name], module, labels[module.Name]))
                .Where(service => service.HasValue)
                .Select(service => service.OrDefault())
                .ToList();

            V1ServiceList currentServices = await this.client.ListNamespacedServiceAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
            await this.ManageServices(currentServices, desiredServices);

            var desiredDeployments = modules
                .Select(module => this.deploymentMapper.CreateDeployment(moduleIdentities[module.Name], module, labels[module.Name]))
                .ToList();

            V1DeploymentList currentDeployments = await this.client.ListNamespacedDeploymentAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
            await this.ManageDeployments(currentDeployments, desiredDeployments);

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
                        return this.client.DeleteNamespacedDeployment1Async(name, this.deviceNamespace);
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

                        this.serviceAccountMapper.Update(update.To, update.From);
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
