// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
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
        readonly IKubernetesServiceProvider serviceProvider;
        readonly IKubernetesDeploymentProvider deploymentProvider;
        readonly IKubernetesServiceAccountProvider serviceAccountProvider;

        public EdgeDeploymentController(
            ResourceName resourceName,
            string deploymentSelector,
            string deviceNamespace,
            IKubernetes client,
            IModuleIdentityLifecycleManager moduleIdentityLifecycleManager,
            IKubernetesServiceProvider serviceProvider,
            IKubernetesDeploymentProvider deploymentProvider,
            IKubernetesServiceAccountProvider serviceAccountProvider)
        {
            this.resourceName = resourceName;
            this.deploymentSelector = deploymentSelector;
            this.deviceNamespace = deviceNamespace;
            this.moduleIdentityLifecycleManager = moduleIdentityLifecycleManager;
            this.client = client;
            this.serviceProvider = serviceProvider;
            this.deploymentProvider = deploymentProvider;
            this.serviceAccountProvider = serviceAccountProvider;
        }

        string DeploymentName(string moduleId) => KubeUtils.SanitizeK8sValue(moduleId);

        public async Task<ModuleSet> DeployModulesAsync(IList<KubernetesModule> modules, ModuleSet currentModules)
        {
            var desiredModules = ModuleSet.Create(modules.ToArray());
            var moduleIdentities = await this.moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desiredModules, currentModules);

            var labels = modules
                .ToDictionary(
                    module => module.Name,
                    module => new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeModuleLabel] = KubeUtils.SanitizeK8sValue(moduleIdentities[module.Name].ModuleId),
                        [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue(this.resourceName.DeviceId),
                        [KubernetesConstants.K8sEdgeHubNameLabel] = KubeUtils.SanitizeLabelValue(this.resourceName.Hostname)
                    });

            var desiredServices = modules
                .Select(module => this.serviceProvider.GetService(moduleIdentities[module.Name], module, labels[module.Name]))
                .Where(service => service.HasValue)
                .Select(service => service.OrDefault())
                .ToList();

            V1ServiceList currentServices = await this.client.ListNamespacedServiceAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
            await this.ManageServices(currentServices, desiredServices);

            var desiredDeployments = modules
                .Select(module => this.deploymentProvider.GetDeployment(moduleIdentities[module.Name], module, labels[module.Name]))
                .ToList();

            V1DeploymentList currentDeployments = await this.client.ListNamespacedDeploymentAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
            await this.ManageDeployments(currentDeployments, desiredDeployments);

            var desiredServiceAccounts = modules
                .Select(module => this.serviceAccountProvider.GetServiceAccount(moduleIdentities[module.Name], module, labels[module.Name]))
                .ToList();

            V1ServiceAccountList currentServiceAccounts = await this.client.ListNamespacedServiceAccountAsync(this.deviceNamespace, labelSelector: this.deploymentSelector);
            await this.ManageServiceAccounts(currentServiceAccounts, desiredServiceAccounts);

            return desiredModules;
        }

        async Task ManageServices(V1ServiceList existing, List<V1Service> desired)
        {
            // find common service names that are candidates to update
            var commonNames = desired.Where(d => existing.Items.Any(e => e.Metadata.Name == d.Metadata.Name))
                .Select(d => d.Metadata.Name)
                .ToList();

            // Compose creation strings from existing items
            Dictionary<string, string> creationStrings = GetServiceConfig(existing);

            // Update only those services if configurations have not matched
            var updating = desired.Where(
                    service =>
                    {
                        // current module is to add it to cluster
                        if (!creationStrings.TryGetValue(service.Metadata.Name, out string creationString))
                        {
                            return false;
                        }

                        // current module is to update an existing one
                        return service.Metadata.Annotations[KubernetesConstants.CreationString] != creationString;
                    })
                .ToList();

            var updatingTask = updating
                .Select(
                    service =>
                    {
                        Events.UpdateService(service);
                        return this.client.ReplaceNamespacedServiceAsync(service, service.Metadata.Name, this.deviceNamespace);
                    });
            await Task.WhenAll(updatingTask);

            // Delete only those existing services that are not in the list of common names
            var removingTasks = existing.Items
                .Where(service => !commonNames.Contains(service.Metadata.Name))
                .Select(
                    service =>
                    {
                        Events.DeleteService(service);
                        return this.client.DeleteNamespacedServiceAsync(service.Metadata.Name, this.deviceNamespace);
                    });
            await Task.WhenAll(removingTasks);

            // Add only those desired services that are not in the list of common names
            var addingTasks = desired
                .Where(service => !commonNames.Contains(service.Metadata.Name))
                .Select(
                    service =>
                    {
                        Events.CreateService(service);
                        return this.client.CreateNamespacedServiceAsync(service, this.deviceNamespace);
                    });
            await Task.WhenAll(addingTasks);
        }

        static Dictionary<string, string> GetServiceConfig(V1ServiceList services)
        {
            var config = new Dictionary<string, string>();
            foreach (var service in services.Items)
            {
                if (service?.Metadata?.Name == null)
                {
                    Events.InvalidCreationString(service == null ? "service" : "name", "null service in list");
                    continue;
                }

                if (service.Metadata?.Annotations == null || !service.Metadata.Annotations.TryGetValue(KubernetesConstants.CreationString, out string creationString))
                {
                    Events.InvalidCreationString(service.Kind, service.Metadata?.Name);

                    var serviceWithoutStatus = new V1Service(service.ApiVersion, service.Kind, service.Metadata, service.Spec);
                    creationString = JsonConvert.SerializeObject(serviceWithoutStatus);
                }

                config[service.Metadata.Name] = creationString;
            }

            return config;
        }

        async Task ManageDeployments(V1DeploymentList existing, List<V1Deployment> desired)
        {
            // find common deployment names that are candidates to update
            var commonNames = desired.Where(d => existing.Items.Any(e => e.Metadata.Name == d.Metadata.Name))
                .Select(d => d.Metadata.Name)
                .ToList();

            // Compose creation strings from existing items
            Dictionary<string, string> creationStrings = GetDeploymentConfig(existing);

            // Update only deployments if configurations have not matched or it's an EdgeAgent with the same version
            var updating = desired.Where(
                    deployment =>
                    {
                        // current module is to add it to cluster
                        if (!creationStrings.TryGetValue(deployment.Metadata.Name, out string creationString))
                        {
                            return false;
                        }

                        // current module is to update an existing one
                        if (deployment.Metadata.Annotations[KubernetesConstants.CreationString] != creationString)
                        {
                            return true;
                        }

                        // current module is not EdgeAgent
                        if (deployment.Metadata.Name != this.DeploymentName(CoreConstants.EdgeAgentModuleName))
                        {
                            return true;
                        }

                        var current = existing.Items.First(e => e.Metadata.Name == deployment.Metadata.Name);
                        return V1DeploymentEx.ImageEquals(current, deployment);
                    })
                .ToList();

            var updatingTask = updating
                .Select(
                    deployment =>
                    {
                        Events.UpdateDeployment(deployment);
                        return this.client.ReplaceNamespacedDeploymentAsync(deployment, deployment.Metadata.Name, this.deviceNamespace);
                    });
            await Task.WhenAll(updatingTask);

            // Delete only those existing deployments that are not in the list of common names
            var removingTasks = existing.Items
                .Where(deployment => !commonNames.Contains(deployment.Metadata.Name))
                .Select(
                    deployment =>
                    {
                        Events.DeleteDeployment(deployment);
                        return this.client.DeleteNamespacedDeployment1Async(deployment.Metadata.Name, this.deviceNamespace);
                    });
            await Task.WhenAll(removingTasks);

            // Add only those desired deployments that are not in the list of common names
            var addingTasks = desired
                .Where(deployment => !commonNames.Contains(deployment.Metadata.Name))
                .Select(
                    deployment =>
                    {
                        Events.CreateDeployment(deployment);
                        return this.client.CreateNamespacedDeploymentAsync(deployment, this.deviceNamespace);
                    });
            await Task.WhenAll(addingTasks);
        }

        static Dictionary<string, string> GetDeploymentConfig(V1DeploymentList deployments)
        {
            var config = new Dictionary<string, string>();
            foreach (var deployment in deployments.Items)
            {
                if (deployment?.Metadata?.Name == null)
                {
                    Events.InvalidCreationString(deployment == null ? "deployment" : "name", "null deployment in list");
                    continue;
                }

                if (deployment.Metadata?.Annotations == null || !deployment.Metadata.Annotations.TryGetValue(KubernetesConstants.CreationString, out string creationString))
                {
                    Events.InvalidCreationString(deployment.Kind, deployment.Metadata?.Name);

                    var deploymentWithoutStatus = new V1Deployment(deployment.ApiVersion, deployment.Kind, deployment.Metadata, deployment.Spec);
                    creationString = JsonConvert.SerializeObject(deploymentWithoutStatus);
                }

                config[deployment.Metadata.Name] = creationString;
            }

            return config;
        }

        async Task ManageServiceAccounts(V1ServiceAccountList existing, IReadOnlyCollection<V1ServiceAccount> desired)
        {
            // find common deployment names that are candidates to update
            var commonNames = desired.Where(d => existing.Items.Any(e => e.Metadata.Name == d.Metadata.Name))
                .Select(d => d.Metadata.Name)
                .ToList();

            // Update only service accounts that have same name among existing ones
            var updating = desired.Where(serviceAccount => commonNames.Contains(serviceAccount.Metadata.Name)).ToList();
            var updatingTasks = updating.Select(
                account =>
                {
                    Events.UpdateServiceAccount(account);
                    return this.client.ReplaceNamespacedServiceAccountAsync(account, account.Metadata.Name, this.deviceNamespace);
                });
            await Task.WhenAll(updatingTasks);

            // Delete only those existing service accounts that are not in in the list of common names
            var removingTasks = existing.Items
                .Where(serviceAccount => !commonNames.Contains(serviceAccount.Metadata.Name))
                .Select(
                    account =>
                    {
                        Events.DeleteServiceAccount(account);
                        return this.client.DeleteNamespacedServiceAccountAsync(account.Metadata.Name, this.deviceNamespace);
                    });
            await Task.WhenAll(removingTasks);

            // Add only those desired service account that are not in the list of common names
            var addingTasks = desired.Where(serviceAccount => !commonNames.Contains(serviceAccount.Metadata.Name))
                .Select(
                    account =>
                    {
                        Events.CreateServiceAccount(account);
                        return this.client.CreateNamespacedServiceAccountAsync(account, this.deviceNamespace);
                    });
            await Task.WhenAll(addingTasks);
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

            public static void DeleteService(V1Service service)
            {
                Log.LogInformation((int)EventIds.DeleteService, $"Delete service {service.Metadata.Name}");
            }

            public static void CreateService(V1Service service)
            {
                Log.LogInformation((int)EventIds.CreateService, $"Create service {service.Metadata.Name}");
            }

            public static void CreateDeployment(V1Deployment deployment)
            {
                Log.LogInformation((int)EventIds.CreateDeployment, $"Create deployment {deployment.Metadata.Name}");
            }

            public static void DeleteDeployment(V1Deployment deployment)
            {
                Log.LogInformation((int)EventIds.DeleteDeployment, $"Delete deployment {deployment.Metadata.Name}");
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

            public static void DeleteServiceAccount(V1ServiceAccount serviceAccount)
            {
                Log.LogDebug((int)EventIds.DeleteServiceAccount, $"Delete Service Account {serviceAccount.Metadata.Name}");
            }

            public static void UpdateServiceAccount(V1ServiceAccount serviceAccount)
            {
                Log.LogDebug((int)EventIds.UpdateServiceAccount, $"Update Service Account {serviceAccount.Metadata.Name}");
            }
        }
    }
}
