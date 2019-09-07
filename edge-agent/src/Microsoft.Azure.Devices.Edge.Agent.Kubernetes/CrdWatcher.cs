// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;
    using Newtonsoft.Json;
    using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;
    using CoreConstants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;

    public class CrdWatcher
    {
        const string EdgeHubHostname = "edgehub";

        readonly IKubernetes client;
        readonly AsyncLock watchLock = new AsyncLock();

        readonly string iotHubHostname;
        readonly string deviceId;
        readonly string edgeHostname;
        readonly string resourceName;
        readonly string deploymentSelector;
        readonly string proxyImage;
        readonly string proxyConfigPath;
        readonly string proxyConfigVolumeName;
        readonly string proxyTrustBundlePath;
        readonly string proxyTrustBundleVolumeName;
        readonly string defaultMapServiceType;
        readonly string workloadApiVersion;
        readonly string k8sNamespace;
        readonly Uri workloadUri;
        readonly Uri managementUri;
        readonly IModuleIdentityLifecycleManager moduleIdentityLifecycleManager;
        ModuleSet currentModules;
        Option<Watcher<EdgeDeploymentDefinition>> operatorWatch;

        public CrdWatcher(
            string iotHubHostname,
            string deviceId,
            string edgeHostname,
            string proxyImage,
            string proxyConfigPath,
            string proxyConfigVolumeName,
            string proxyTrustBundlePath,
            string proxyTrustBundleVolumeName,
            string resourceName,
            string deploymentSelector,
            string defaultMapServiceType,
            string k8sNamespace,
            string workloadApiVersion,
            Uri workloadUri,
            Uri managementUri,
            IModuleIdentityLifecycleManager moduleIdentityLifecycleManager,
            IKubernetes client)
        {
            this.iotHubHostname = iotHubHostname;
            this.deviceId = deviceId;
            this.edgeHostname = edgeHostname;
            this.proxyImage = proxyImage;
            this.proxyConfigPath = proxyConfigPath;
            this.proxyConfigVolumeName = proxyConfigVolumeName;
            this.proxyTrustBundlePath = proxyTrustBundlePath;
            this.proxyTrustBundleVolumeName = proxyTrustBundleVolumeName;
            this.resourceName = resourceName;
            this.deploymentSelector = deploymentSelector;
            this.defaultMapServiceType = defaultMapServiceType;
            this.k8sNamespace = k8sNamespace;
            this.workloadApiVersion = workloadApiVersion;
            this.workloadUri = workloadUri;
            this.managementUri = managementUri;
            this.moduleIdentityLifecycleManager = moduleIdentityLifecycleManager;
            this.client = client;
            this.currentModules = ModuleSet.Empty;
        }

        public async Task ListCrdComplete(Task<HttpOperationResponse<object>> customObjectWatchTask)
        {
            if (customObjectWatchTask == null)
            {
                Events.NullListResponse("ListNamespacedCustomObjectWithHttpMessagesAsync", "task");
                throw new NullReferenceException("Null Task from ListNamespacedCustomObjectWithHttpMessagesAsync");
            }
            else
            {
                HttpOperationResponse<object> customObjectWatch = await customObjectWatchTask;
                if (customObjectWatch != null)
                {
                    // We can add events to a watch once created, like if connection is closed, etc.
                    this.operatorWatch = Option.Some(
                        customObjectWatch.Watch<EdgeDeploymentDefinition>(
                            onEvent: async (type, item) =>
                            {
                                try
                                {
                                    await this.WatchDeploymentEventsAsync(type, item);
                                }
                                catch (Exception ex) when (!ex.IsFatal())
                                {
                                    Events.ExceptionInCustomResourceWatch(ex);
                                }
                            },
                            onClosed: () =>
                            {
                                Events.CrdWatchClosed();

                                // get rid of the current crd watch object since we got closed
                                this.operatorWatch.ForEach(watch => watch.Dispose());
                                this.operatorWatch = Option.None<Watcher<EdgeDeploymentDefinition>>();

                                // kick off a new watch
                                this.client.ListNamespacedCustomObjectWithHttpMessagesAsync(
                                    Constants.K8sCrdGroup,
                                    Constants.K8sApiVersion,
                                    this.k8sNamespace,
                                    Constants.K8sCrdPlural,
                                    watch: true).ContinueWith(this.ListCrdComplete);
                            },
                            onError: Events.ExceptionInCustomResourceWatch));
                }
                else
                {
                    Events.NullListResponse("ListNamespacedCustomObjectWithHttpMessagesAsync", "http response");
                    throw new NullReferenceException("Null response from ListNamespacedCustomObjectWithHttpMessagesAsync");
                }
            }
        }


        internal async Task WatchDeploymentEventsAsync(WatchEventType type, EdgeDeploymentDefinition edgeDeploymentDefinition)
        {
            // only operate on the device that matches this operator.
            if (string.Equals(edgeDeploymentDefinition.Metadata.Name, this.resourceName, StringComparison.OrdinalIgnoreCase))
            {
                using (await this.watchLock.LockAsync())
                {
                    V1ServiceList currentServices = await this.client.ListNamespacedServiceAsync(this.k8sNamespace, labelSelector: this.deploymentSelector);
                    V1DeploymentList currentDeployments = await this.client.ListNamespacedDeploymentAsync(this.k8sNamespace, labelSelector: this.deploymentSelector);
                    Events.DeploymentStatus(type, this.resourceName);
                    switch (type)
                    {
                        case WatchEventType.Added:
                        case WatchEventType.Modified:
                            await this.UpsertDeployments(currentServices, currentDeployments, edgeDeploymentDefinition.Spec);
                            break;

                        case WatchEventType.Deleted:
                            await this.HandleEdgeDeploymentDeleted(currentServices, currentDeployments);
                            break;

                        case WatchEventType.Error:
                            Events.DeploymentError();
                            break;
                    }
                }
            }
            else
            {
                Events.DeploymentNameMismatch(edgeDeploymentDefinition.Metadata.Name, this.resourceName);
            }
        }

        private string DeploymentName(string moduleId) => KubeUtils.SanitizeK8sValue(moduleId);

        private async Task HandleEdgeDeploymentDeleted(V1ServiceList currentServices, V1DeploymentList currentDeployments)
        {
            // Delete the deployment.
            // Delete any services.
            IEnumerable<Task<V1Status>> removeServiceTasks = currentServices.Items.Select(i => this.client.DeleteNamespacedServiceAsync(i.Metadata.Name, this.k8sNamespace, new V1DeleteOptions()));
            await Task.WhenAll(removeServiceTasks);
            IEnumerable<Task<V1Status>> removeDeploymentTasks = currentDeployments.Items.Select(
                d => this.client.DeleteNamespacedDeployment1Async(
                    d.Metadata.Name,
                    this.k8sNamespace,
                    new V1DeleteOptions(propagationPolicy: "Foreground"),
                    propagationPolicy: "Foreground"));

            // Remove the service account for all deployments
            var serviceAccountNames = currentDeployments.Items.Select(deployment => deployment.Metadata.Name);
            await this.PruneServiceAccounts(serviceAccountNames.ToList());

            await Task.WhenAll(removeDeploymentTasks);
            this.currentModules = ModuleSet.Empty;
        }

        private async Task UpsertDeployments(V1ServiceList currentServices, V1DeploymentList currentDeployments, IList<KubernetesModule> spec)
        {
            var desiredModules = ModuleSet.Create(spec.ToArray());
            var moduleIdentities = await this.moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desiredModules, this.currentModules);

            var desiredServices = new List<V1Service>();
            var desiredDeployments = new List<V1Deployment>();

            // Bootstrap the module builder
            var kubernetesModelBuilder = new KubernetesModelBuilder(this.proxyImage, this.proxyConfigPath, this.proxyConfigVolumeName, this.proxyTrustBundlePath, this.proxyTrustBundleVolumeName, this.defaultMapServiceType);

            foreach (KubernetesModule module in spec)
            {
                var moduleId = moduleIdentities[module.Name];

                string deploymentName = this.DeploymentName(moduleIdentities[module.Name].ModuleId);
                if (string.Equals(module.Type, "docker"))
                {
                    // Default labels
                    var labels = new Dictionary<string, string>
                    {
                        [Constants.K8sEdgeModuleLabel] = deploymentName,
                        [Constants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue(this.deviceId),
                        [Constants.K8sEdgeHubNameLabel] = KubeUtils.SanitizeLabelValue(this.iotHubHostname)
                    };

                    // Create a Pod for each module, and a proxy container.

                    List<V1EnvVar> envVars = this.CollectEnv(module, moduleId);

                    // Load the current module
                    kubernetesModelBuilder.LoadModule(labels, module, moduleId, envVars);

                    // Create a Service for every network interface of each module. (label them with hub, device and module id)
                    Option<V1Service> moduleService = kubernetesModelBuilder.GetService();
                    moduleService.ForEach(service => desiredServices.Add(service));

                    // Get the converted pod
                    V1PodTemplateSpec v1PodSpec = kubernetesModelBuilder.GetPod();

                    // Deployment data
                    var deploymentMeta = new V1ObjectMeta(name: deploymentName, labels: labels);

                    var selector = new V1LabelSelector(matchLabels: labels);
                    var deploymentSpec = new V1DeploymentSpec(replicas: 1, selector: selector, template: v1PodSpec);

                    desiredDeployments.Add(new V1Deployment(metadata: deploymentMeta, spec: deploymentSpec));
                }
                else
                {
                    Events.InvalidModuleType(module);
                }
            }

            await this.ManageServices(currentServices, desiredServices);

            await this.ManageDeployments(currentDeployments, desiredDeployments);

            this.currentModules = desiredModules;
        }

        async Task ManageServices(V1ServiceList currentServices, List<V1Service> desiredServices)
        {
            Dictionary<string, string> currentV1ServicesFromAnnotations = this.GetCurrentServiceConfig(currentServices);

            // Figure out what to remove
            var servicesRemoved = new List<V1Service>(currentServices.Items);
            servicesRemoved.RemoveAll(s => desiredServices.Exists(i => string.Equals(i.Metadata.Name, s.Metadata.Name)));

            // Figure out what to create
            var newServices = new List<V1Service>();
            desiredServices.ForEach(
                service =>
                {
                    string creationString = JsonConvert.SerializeObject(service);

                    if (currentV1ServicesFromAnnotations.ContainsKey(service.Metadata.Name))
                    {
                        string serviceAnnotation = currentV1ServicesFromAnnotations[service.Metadata.Name];
                        // If configuration matches, no need to update service
                        if (string.Equals(serviceAnnotation, creationString))
                        {
                            return;
                        }

                        if (service.Metadata.Annotations == null)
                        {
                            service.Metadata.Annotations = new Dictionary<string, string>();
                        }

                        service.Metadata.Annotations[Constants.CreationString] = creationString;

                        servicesRemoved.Add(service);
                        newServices.Add(service);
                        Events.UpdateService(service.Metadata.Name);
                    }
                    else
                    {
                        if (service.Metadata.Annotations == null)
                        {
                            service.Metadata.Annotations = new Dictionary<string, string>();
                        }

                        service.Metadata.Annotations[Constants.CreationString] = creationString;

                        newServices.Add(service);
                        Events.CreateService(service.Metadata.Name);
                    }
                });

            // remove the old
            await Task.WhenAll(
                servicesRemoved.Select(
                    i =>
                    {
                        Events.DeletingService(i);
                        return this.client.DeleteNamespacedServiceAsync(i.Metadata.Name, this.k8sNamespace, new V1DeleteOptions());
                    }));

            // Create the new.
            await Task.WhenAll(
                newServices.Select(
                    s =>
                    {
                        Events.CreatingService(s);
                        return this.client.CreateNamespacedServiceAsync(s, this.k8sNamespace);
                    }));
        }

        async Task ManageDeployments(V1DeploymentList currentDeployments, List<V1Deployment> desiredDeployments)
        {
            Dictionary<string, string> currentDeploymentsFromAnnotations = this.GetCurrentDeploymentConfig(currentDeployments)
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value);

            var deploymentsRemoved = new List<V1Deployment>(currentDeployments.Items);
            deploymentsRemoved.RemoveAll(
                removedDeployment => { return desiredDeployments.Exists(deployment => string.Equals(deployment.Metadata.Name, removedDeployment.Metadata.Name)); });
            var deploymentsUpdated = new List<V1Deployment>();
            var newDeployments = new List<V1Deployment>();
            List<V1Deployment> currentDeploymentsList = currentDeployments.Items.ToList();
            desiredDeployments.ForEach(
                deployment =>
                {
                    if (currentDeploymentsFromAnnotations.ContainsKey(deployment.Metadata.Name))
                    {
                        V1Deployment current = currentDeploymentsList.Find(i => string.Equals(i.Metadata.Name, deployment.Metadata.Name));
                        string currentFromAnnotation = currentDeploymentsFromAnnotations[deployment.Metadata.Name];
                        string creationString = JsonConvert.SerializeObject(deployment);

                        // If configuration matches, or this is edgeAgent deployment and the images match,
                        // no need to do update deployment
                        if (string.Equals(currentFromAnnotation, creationString) ||
                            (string.Equals(deployment.Metadata.Name, this.DeploymentName(CoreConstants.EdgeAgentModuleName)) && V1DeploymentEx.ImageEquals(current, deployment)))
                        {
                            return;
                        }

                        deployment.Metadata.ResourceVersion = current.Metadata.ResourceVersion;
                        if (deployment.Metadata.Annotations == null)
                        {
                            var annotations = new Dictionary<string, string>
                            {
                                [Constants.CreationString] = creationString
                            };
                            deployment.Metadata.Annotations = annotations;
                        }
                        else
                        {
                            deployment.Metadata.Annotations[Constants.CreationString] = creationString;
                        }

                        deploymentsUpdated.Add(deployment);
                        Events.UpdateDeployment(deployment.Metadata.Name);
                    }
                    else
                    {
                        string creationString = JsonConvert.SerializeObject(deployment);
                        var annotations = new Dictionary<string, string>
                        {
                            [Constants.CreationString] = creationString
                        };
                        deployment.Metadata.Annotations = annotations;
                        newDeployments.Add(deployment);
                        Events.CreateDeployment(deployment.Metadata.Name);
                    }
                });

            // First we must delete existing service accounts since service account does not support an update operation.
            // This needs to block so that we can re-create the new accounts below without a collision.
            // We will also take this opportunity to delete the accounts that have their deployment being deleted.
            var deletedOrUpdatedDeploymentNames = deploymentsRemoved.Concat(newDeployments).Select(deployment => deployment.Metadata.Name);
            await this.PruneServiceAccounts(deletedOrUpdatedDeploymentNames.ToList());

            // Remove the old
            var removeDeploymentsTasks = deploymentsRemoved.Select(
                deployment =>
                {
                    Events.DeletingDeployment(deployment);
                    return this.client.DeleteNamespacedDeployment1Async(deployment.Metadata.Name, this.k8sNamespace, new V1DeleteOptions(propagationPolicy: "Foreground"), propagationPolicy: "Foreground");
                });

            // Create the new deployments
            var createDeploymentsTasks = newDeployments.Select(
                deployment =>
                {
                    Events.CreatingDeployment(deployment);
                    return this.client.CreateNamespacedDeploymentAsync(deployment, this.k8sNamespace);
                });

            // Create the new Service Accounts
            var createServiceAccounts = newDeployments.Select(
                deployment =>
                {
                    Events.CreatingServiceAccount(deployment.Metadata.Name);
                    return this.CreateServiceAccount(deployment);
                });

            // Update the existing - should only do this when different.
            var updateDeploymentsTasks = deploymentsUpdated.Select(deployment => this.client.ReplaceNamespacedDeploymentAsync(deployment, deployment.Metadata.Name, this.k8sNamespace));

            await Task.WhenAll(removeDeploymentsTasks);
            await Task.WhenAll(createDeploymentsTasks);
            await Task.WhenAll(createServiceAccounts);
            await Task.WhenAll(updateDeploymentsTasks);

            return;
        }

        Task<V1ServiceAccount> CreateServiceAccount(V1Deployment deployment)
        {
            V1ServiceAccount account = new V1ServiceAccount();
            var metadata = new V1ObjectMeta();

            string moduleId = deployment.Metadata.Labels[Constants.K8sEdgeModuleLabel];
            metadata.Labels = deployment.Metadata.Labels;
            metadata.Annotations = new Dictionary<string, string>
            {
                [Constants.K8sEdgeOriginalModuleId] = moduleId
            };

            metadata.Name = moduleId;

            account.Metadata = metadata;

            return this.client.CreateNamespacedServiceAccountAsync(account, this.k8sNamespace);
        }

        async Task PruneServiceAccounts(List<string> accountNamesToPrune)
        {
            var currentServiceAccounts = (await this.client.ListNamespacedServiceAccountAsync(this.k8sNamespace)).Items;

            // Prune down the list to those found in the passed in prune list.
            var accountsToDelete = currentServiceAccounts.Where(serviceAccount => accountNamesToPrune.Contains(serviceAccount.Metadata.Name));

            var deletionTasks = accountsToDelete.Select(
                account =>
                {
                    Events.DeletingServiceAccount(account.Metadata.Name);
                    return this.client.DeleteNamespacedServiceAccountAsync(account.Metadata.Name, this.k8sNamespace);
                });

            await Task.WhenAll(deletionTasks);
        }

        List<V1EnvVar> CollectEnv(IModule<AgentDocker.CombinedDockerConfig> moduleWithDockerConfig, IModuleIdentity identity)
        {
            char[] envSplit = { '=' };
            var envList = new List<V1EnvVar>();
            foreach (KeyValuePair<string, EnvVal> item in moduleWithDockerConfig.Env)
            {
                envList.Add(new V1EnvVar(item.Key, item.Value.Value));
            }

            if (moduleWithDockerConfig.Config?.CreateOptions?.Env != null)
            {
                foreach (string hostEnv in moduleWithDockerConfig.Config?.CreateOptions?.Env)
                {
                    string[] keyValue = hostEnv.Split(envSplit, 2);
                    if (keyValue.Count() == 2)
                    {
                        envList.Add(new V1EnvVar(keyValue[0], keyValue[1]));
                    }
                }
            }

            envList.Add(new V1EnvVar(CoreConstants.IotHubHostnameVariableName, this.iotHubHostname));
            envList.Add(new V1EnvVar(CoreConstants.EdgeletAuthSchemeVariableName, "sasToken"));
            envList.Add(new V1EnvVar(Logger.RuntimeLogLevelEnvKey, Logger.GetLogLevel().ToString()));
            envList.Add(new V1EnvVar(CoreConstants.EdgeletWorkloadUriVariableName, this.workloadUri.ToString()));
            if (identity.Credentials is IdentityProviderServiceCredentials creds)
            {
                envList.Add(new V1EnvVar(CoreConstants.EdgeletModuleGenerationIdVariableName, creds.ModuleGenerationId));
            }
            envList.Add(new V1EnvVar(CoreConstants.DeviceIdVariableName, this.deviceId)); // could also get this from module identity
            envList.Add(new V1EnvVar(CoreConstants.ModuleIdVariableName, identity.ModuleId));
            envList.Add(new V1EnvVar(CoreConstants.EdgeletApiVersionVariableName, this.workloadApiVersion));

            if (string.Equals(identity.ModuleId, CoreConstants.EdgeAgentModuleIdentityName))
            {
                envList.Add(new V1EnvVar(CoreConstants.ModeKey, CoreConstants.KubernetesMode));
                envList.Add(new V1EnvVar(CoreConstants.EdgeletManagementUriVariableName, this.managementUri.ToString()));
                envList.Add(new V1EnvVar(CoreConstants.NetworkIdKey, "azure-iot-edge"));
                envList.Add(new V1EnvVar(CoreConstants.ProxyImageEnvKey, this.proxyImage));
                envList.Add(new V1EnvVar(CoreConstants.ProxyConfigPathEnvKey, this.proxyConfigPath));
                envList.Add(new V1EnvVar(CoreConstants.ProxyConfigVolumeEnvKey, this.proxyConfigVolumeName));
            }

            if (string.Equals(identity.ModuleId, CoreConstants.EdgeAgentModuleIdentityName) ||
                string.Equals(identity.ModuleId, CoreConstants.EdgeHubModuleIdentityName))
            {
                envList.Add(new V1EnvVar(CoreConstants.EdgeDeviceHostNameKey, this.edgeHostname));
            }
            else
            {
                envList.Add(new V1EnvVar(CoreConstants.GatewayHostnameVariableName, EdgeHubHostname));
            }

            return envList;
        }

        private Dictionary<string, string> GetCurrentServiceConfig(V1ServiceList currentServices)
        {
            return currentServices.Items.ToDictionary(
                service =>
                {
                    if (service?.Metadata?.Name != null)
                    {
                        return service.Metadata.Name;
                    }

                    Events.InvalidCreationString("service", "null service");
                    throw new NullReferenceException("null service in list");
                },
                service =>
                {
                    if (service == null)
                    {
                        Events.InvalidCreationString("service", "null service");
                        throw new NullReferenceException("null service in list");
                    }

                    if (service.Metadata?.Annotations != null
                        && service.Metadata.Annotations.TryGetValue(Constants.CreationString, out string creationString))
                    {
                        return creationString;
                    }

                    Events.InvalidCreationString(service.Kind, service.Metadata?.Name);

                    var serviceWithoutStatus = new V1Service(service.ApiVersion, service.Kind, service.Metadata, service.Spec);
                    return JsonConvert.SerializeObject(serviceWithoutStatus);
                });
        }

        private Dictionary<string, string> GetCurrentDeploymentConfig(V1DeploymentList currentDeployments)
        {
            return currentDeployments.Items.ToDictionary(
                deployment =>
                {
                    if (deployment?.Metadata?.Name != null)
                    {
                        return deployment.Metadata.Name;
                    }

                    Events.InvalidCreationString("deployment", "null deployment");
                    throw new NullReferenceException("null deployment in list");
                },
                deployment =>
                {
                    if (deployment == null)
                    {
                        Events.InvalidCreationString("deployment", "null deployment");
                        throw new NullReferenceException("null deployment in list");
                    }

                    if (deployment.Metadata?.Annotations != null
                        && deployment.Metadata.Annotations.TryGetValue(Constants.CreationString, out string creationString))
                    {
                        return creationString;
                    }

                    Events.InvalidCreationString(deployment.Kind, deployment.Metadata?.Name);
                    var deploymentWithoutStatus = new V1Deployment(deployment.ApiVersion, deployment.Kind, deployment.Metadata, deployment.Spec);
                    return JsonConvert.SerializeObject(deploymentWithoutStatus);
                });
        }

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesCrdWatcher;
            private static readonly ILogger Log = Logger.Factory.CreateLogger<CrdWatcher>();

            enum EventIds
            {
                InvalidModuleType = IdStart,
                ExceptionInCustomResourceWatch,
                InvalidCreationString,
                EdgeDeploymentDeserializeFail,
                DeploymentStatus,
                DeploymentError,
                DeploymentNameMismatch,
                UpdateService,
                CreateService,
                UpdateDeployment,
                CreateDeployment,
                NullListResponse,
                DeletingService,
                DeletingDeployment,
                CreatingDeployment,
                CreatingService,
                ReplacingDeployment,
                CrdWatchClosed,
                CreatingServiceAccount,
                DeletingServiceAccount
            }

            public static void DeletingService(V1Service service)
            {
                Log.LogInformation((int)EventIds.DeletingService, $"Deleting service {service.Metadata.Name}");
            }

            public static void CreatingService(V1Service service)
            {
                Log.LogInformation((int)EventIds.CreatingService, $"Creating service {service.Metadata.Name}");
            }

            public static void DeletingDeployment(V1Deployment deployment)
            {
                Log.LogInformation((int)EventIds.DeletingDeployment, $"Deleting deployment {deployment.Metadata.Name}");
            }

            public static void CreatingDeployment(V1Deployment deployment)
            {
                Log.LogInformation((int)EventIds.CreatingDeployment, $"Creating deployment {deployment.Metadata.Name}");
            }

            public static void InvalidModuleType(IModule module)
            {
                Log.LogError((int)EventIds.InvalidModuleType, $"Module {module.Name} has an invalid module type '{module.Type}'. Expected type 'docker'");
            }

            public static void ExceptionInCustomResourceWatch(Exception ex)
            {
                Log.LogError((int)EventIds.ExceptionInCustomResourceWatch, ex, "Exception caught in Custom Resource Watch task.");
            }

            public static void InvalidCreationString(string kind, string name)
            {
                Log.LogDebug((int)EventIds.InvalidCreationString, $"Expected a valid '{kind}' creation string in k8s Object '{name}'.");
            }

            public static void EdgeDeploymentDeserializeFail(Exception e)
            {
                Log.LogError((int)EventIds.EdgeDeploymentDeserializeFail, e, "Received an invalid Edge Deployment.");
            }

            public static void DeploymentStatus(WatchEventType type, string name)
            {
                Log.LogDebug((int)EventIds.DeploymentStatus, $"Deployment '{name}', status'{type}'");
            }

            public static void DeploymentError()
            {
                Log.LogError((int)EventIds.DeploymentError, "Operator received error on watch type.");
            }

            public static void DeploymentNameMismatch(string received, string expected)
            {
                Log.LogDebug((int)EventIds.DeploymentNameMismatch, $"Watching for edge deployments for '{expected}', received notification for '{received}'");
            }

            public static void UpdateService(string name)
            {
                Log.LogDebug((int)EventIds.UpdateService, $"Updating service object '{name}'");
            }

            public static void CreateService(string name)
            {
                Log.LogDebug((int)EventIds.CreateService, $"Creating service object '{name}'");
            }

            public static void UpdateDeployment(string name)
            {
                Log.LogDebug((int)EventIds.UpdateDeployment, $"Updating edge deployment '{name}'");
            }

            public static void CreateDeployment(string name)
            {
                Log.LogDebug((int)EventIds.CreateDeployment, $"Creating edge deployment '{name}'");
            }

            public static void CreatingServiceAccount(string name)
            {
                Log.LogDebug((int)EventIds.CreatingServiceAccount, $"Creating Service Account {name}");
            }

            public static void DeletingServiceAccount(string name)
            {
                Log.LogDebug((int)EventIds.DeletingServiceAccount, $"Deleting Service Account {name}");
            }

            public static void NullListResponse(string listType, string what)
            {
                Log.LogError((int)EventIds.NullListResponse, $"{listType} returned null {what}");
            }

            public static void CrdWatchClosed()
            {
                Log.LogInformation((int)EventIds.CrdWatchClosed, $"K8s closed the CRD watch. Attempting to reopen watch.");
            }
        }
    }
}
