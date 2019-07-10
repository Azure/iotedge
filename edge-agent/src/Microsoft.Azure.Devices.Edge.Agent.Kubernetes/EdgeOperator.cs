using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Azure.Devices.Edge.Agent.Core;
using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Azure.Devices.Edge.Util.Concurrency;
using Microsoft.Rest;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.Extensions.Logging;
using DockerModels = global::Docker.DotNet.Models;
using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;
using CoreConstants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;
using VolumeOptions =
    Microsoft.Azure.Devices.Edge.Util.Option<(System.Collections.Generic.List<k8s.Models.V1Volume>,
        System.Collections.Generic.List<k8s.Models.V1VolumeMount>, System.Collections.Generic.List<k8s.Models.V1VolumeMount>)>;
using System.IO;

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    public class EdgeOperator<TConfig> : IKubernetesOperator, IRuntimeInfoProvider
    {
        public const string SocketDir = "/var/run/iotedge";
        public const string SocketVolumeName = "workload";
        public const string EdgeHubHostname = "edgehub";
        public const string ConfigVolumeName = "config-volume";

        readonly IKubernetes client;

        Option<Watcher<V1Pod>> podWatch;
        readonly Dictionary<string, ModuleRuntimeInfo> moduleRuntimeInfos;
        readonly AsyncLock moduleLock;
        Option<Watcher<object>> operatorWatch;
        readonly string iotHubHostname;
        readonly string deviceId;
        readonly string edgeHostname;
        readonly string resourceName;
        readonly string deploymentSelector;
        readonly string proxyImage;
        readonly string proxyConfigPath;
        readonly string proxyConfigVolumeName;
        readonly string serviceAccountName;
        readonly Uri workloadUri;
        readonly Uri managementUri;
        readonly string defaultMapServiceType;
        readonly TypeSpecificSerDe<EdgeDeploymentDefinition<TConfig>> deploymentSerde;
        readonly JsonSerializerSettings crdSerializerSettings;
        readonly AsyncLock watchLock;
        readonly IModuleIdentityLifecycleManager moduleIdentityLifecycleManager;
        ModuleSet currentModules;

        public EdgeOperator(
            string iotHubHostname,
            string deviceId,
            string edgeHostname,
            string proxyImage,
            string proxyConfigPath,
            string proxyConfigVolumeName,
            string serviceAccountName,
            Uri workloadUri,
            Uri managementUri,
            PortMapServiceType defaultMapServiceType,
            IKubernetes client,
            IModuleIdentityLifecycleManager moduleIdentityLifecycleManager)
        {
            this.iotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.edgeHostname = Preconditions.CheckNonWhiteSpace(edgeHostname, nameof(edgeHostname));
            this.proxyImage = Preconditions.CheckNonWhiteSpace(proxyImage, nameof(proxyImage));
            this.proxyConfigPath = Preconditions.CheckNonWhiteSpace(proxyConfigPath, nameof(proxyConfigPath));
            this.proxyConfigVolumeName = Preconditions.CheckNonWhiteSpace(proxyConfigVolumeName, nameof(proxyConfigVolumeName));
            this.serviceAccountName = Preconditions.CheckNonWhiteSpace(serviceAccountName, nameof(serviceAccountName));
            this.workloadUri = Preconditions.CheckNotNull(workloadUri, nameof(workloadUri));
            this.managementUri = Preconditions.CheckNotNull(managementUri, nameof(managementUri));
            this.defaultMapServiceType = Preconditions.CheckNotNull(defaultMapServiceType, nameof(defaultMapServiceType)).ToString();
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.moduleRuntimeInfos = new Dictionary<string, ModuleRuntimeInfo>();
            this.moduleLock = new AsyncLock();
            this.watchLock = new AsyncLock();
            this.podWatch = Option.None<Watcher<V1Pod>>();
            this.resourceName = KubeUtils.SanitizeK8sValue(this.iotHubHostname) + Constants.K8sNameDivider + KubeUtils.SanitizeK8sValue(this.deviceId);
            this.deploymentSelector = Constants.K8sEdgeDeviceLabel + " = " + KubeUtils.SanitizeK8sValue(this.deviceId) + "," + Constants.K8sEdgeHubNameLabel + "=" + KubeUtils.SanitizeK8sValue(this.iotHubHostname);
            var deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>
            {
                [typeof(IModule)] = new Dictionary<string, Type>
                {
                    ["docker"] = typeof(CombinedDockerModule)
                }
            };

            this.deploymentSerde = new TypeSpecificSerDe<EdgeDeploymentDefinition<TConfig>>(deserializerTypesMap, new CamelCasePropertyNamesContractResolver());
            this.crdSerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            this.moduleIdentityLifecycleManager = moduleIdentityLifecycleManager;
            this.currentModules = ModuleSet.Empty;
        }

        public Task CloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            this.podWatch.ForEach(watch => watch.Dispose());
            this.operatorWatch.ForEach(watch => watch.Dispose());
        }

        public async Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken cancellationToken)
        {
            using (await this.moduleLock.LockAsync(cancellationToken))
            {
                return this.moduleRuntimeInfos.Select(kvp => kvp.Value);
            }
        }

        public async Task<Stream> GetModuleLogs(string module, bool follow, Option<int> tail, Option<int> since, CancellationToken cancellationToken)
        {
            return await this.client.ReadNamespacedPodLogAsync(
                module,
                Constants.K8sNamespace,
                follow: follow,
                tailLines: tail.GetOrElse(null),
                sinceSeconds: since.GetOrElse(null),
                cancellationToken: cancellationToken);
        }

        public async Task<SystemInfo> GetSystemInfo()
        {
            V1NodeList k8SNodes = await this.client.ListNodeAsync();
            V1Node firstNode = k8SNodes.Items.FirstOrDefault();
            if (firstNode != default(V1Node))
            {
                return new SystemInfo(firstNode.Status.NodeInfo.OperatingSystem, firstNode.Status.NodeInfo.Architecture, firstNode.Status.NodeInfo.OsImage);
            }

            return new SystemInfo(string.Empty, string.Empty, string.Empty);
        }

        public void Start()
        {
            // The following "List..." requests do not return until there is something to return, so if we "await" here,
            // there is a chance that one or both of these requests will block forever - we won't start creating these pods and CRDs
            // until we receive a deployment.
            // Considering setting up these watches is critical to the operation of EdgeAgent, throwing an exception and letting the process crash
            // is an acceptable fate if these tasks fail.

            // Pod watching for module runtime status.
            this.client.ListNamespacedPodWithHttpMessagesAsync(KubeUtils.K8sNamespace, watch: true).ContinueWith(this.ListPodComplete);

            // CRD watch
            this.client.ListClusterCustomObjectWithHttpMessagesAsync(Constants.K8sCrdGroup, Constants.K8sApiVersion, Constants.K8sCrdPlural, watch: true).ContinueWith(this.ListCrdComplete);
        }

        private async Task ListCrdComplete(Task<HttpOperationResponse<object>> customObjectWatchTask)
        {
            if (customObjectWatchTask != null)
            {
                HttpOperationResponse<object> customObjectWatch = await customObjectWatchTask;
                if (customObjectWatch != null)
                {
                    // We can add events to a watch once created, like if connection is closed, etc.
                    this.operatorWatch = Option.Some(
                        customObjectWatch.Watch<object>(
                            onEvent: async (type, item) =>
                            {
                                try
                                {
                                    await this.WatchDeploymentEventsAsync(type, item);
                                }
                                catch (Exception ex) when (!ex.IsFatal())
                                {
                                    KubernetesEventLogger<TConfig>.ExceptionInCustomResourceWatch(ex);
                                }
                            },
                            onClosed: () =>
                            {
                                KubernetesEventLogger<TConfig>.CrdWatchClosed();

                                // get rid of the current crd watch object since we got closed
                                this.operatorWatch.ForEach(watch => watch.Dispose());
                                this.operatorWatch = Option.None<Watcher<object>>();

                                // kick off a new watch
                                this.client.ListClusterCustomObjectWithHttpMessagesAsync(
                                    Constants.K8sCrdGroup,
                                    Constants.K8sApiVersion,
                                    Constants.K8sCrdPlural,
                                    watch: true).ContinueWith(ListCrdComplete);
                            },
                            onError: KubernetesEventLogger<TConfig>.ExceptionInCustomResourceWatch
                        ));
                }
                else
                {
                    KubernetesEventLogger<TConfig>.NullListResponse("ListClusterCustomObjectWithHttpMessagesAsync", "http response");
                    throw new NullReferenceException("Null response from ListClusterCustomObjectWithHttpMessagesAsync");
                }
            }
            else
            {
                KubernetesEventLogger<TConfig>.NullListResponse("ListClusterCustomObjectWithHttpMessagesAsync", "task");
                throw new NullReferenceException("Null Task from ListClusterCustomObjectWithHttpMessagesAsync");
            }
        }

        private async Task WatchDeploymentEventsAsync(WatchEventType type, object custom)
        {
            using (await this.watchLock.LockAsync())
            {
                EdgeDeploymentDefinition<TConfig> customObject;
                try
                {
                    string customString = JsonConvert.SerializeObject(custom);
                    customObject = this.deploymentSerde.Deserialize(customString);
                }
                catch (Exception e)
                {
                    KubernetesEventLogger<TConfig>.EdgeDeploymentDeserializeFail(e);
                    return;
                }

                // only operate on the device that matches this operator.
                if (String.Equals(customObject.Metadata.Name, this.resourceName, StringComparison.OrdinalIgnoreCase))
                {
                    V1ServiceList currentServices = await this.client.ListNamespacedServiceAsync(KubeUtils.K8sNamespace, labelSelector: this.deploymentSelector);
                    V1DeploymentList currentDeployments = await this.client.ListNamespacedDeploymentAsync(KubeUtils.K8sNamespace, labelSelector: this.deploymentSelector);
                    KubernetesEventLogger<TConfig>.DeploymentStatus(type, this.resourceName);
                    switch (type)
                    {
                        case WatchEventType.Added:
                        case WatchEventType.Modified:
                            this.ManageDeployments(currentServices, currentDeployments, customObject);
                            break;

                        case WatchEventType.Deleted:
                        {
                            // Delete the deployment.
                            // Delete any services.
                            IEnumerable<Task<V1Status>> removeServiceTasks = currentServices.Items.Select(i => this.client.DeleteNamespacedServiceAsync(i.Metadata.Name, KubeUtils.K8sNamespace, new V1DeleteOptions()));
                            await Task.WhenAll(removeServiceTasks);
                            IEnumerable<Task<V1Status>> removeDeploymentTasks = currentDeployments.Items.Select(
                                d => this.client.DeleteNamespacedDeployment1Async(
                                    d.Metadata.Name,
                                    KubeUtils.K8sNamespace,
                                    new V1DeleteOptions(propagationPolicy: "Foreground"),
                                    propagationPolicy: "Foreground"));
                            await Task.WhenAll(removeDeploymentTasks);
                        }
                            break;
                        case WatchEventType.Error:
                            KubernetesEventLogger<TConfig>.DeploymentError();
                            break;
                    }
                }
                else
                {
                    KubernetesEventLogger<TConfig>.DeploymentNameMismatch(customObject.Metadata.Name, this.resourceName);
                }
            }
        }

        private string DeploymentName(string moduleId) => KubeUtils.SanitizeK8sValue(moduleId);

        private async void ManageDeployments(V1ServiceList currentServices, V1DeploymentList currentDeployments, EdgeDeploymentDefinition<TConfig> customObject)
        {
            var desiredModules = ModuleSet.Create(customObject.Spec.ToArray());
            var moduleIdentities = await this.moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(desiredModules, this.currentModules);

            // Pull current configuration from annotations.
            Dictionary<string, string> currentV1ServicesFromAnnotations = this.GetCurrentServiceConfig(currentServices);
            // strip out edgeAgent so edgeAgent doesn't update itself.
            // TODO: remove this filter.
            var agentDeploymentName = this.DeploymentName(CoreConstants.EdgeAgentModuleName);
            Dictionary<string, string> currentDeploymentsFromAnnotations = this.GetCurrentDeploymentConfig(currentDeployments)
                .Where(pair => pair.Key != agentDeploymentName)
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value);

            var desiredServices = new List<V1Service>();
            var desiredDeployments = new List<V1Deployment>();
            foreach (KubernetesModule<TConfig> module in customObject.Spec)
            {
                var moduleId = moduleIdentities[module.Name];
                if (string.Equals(module.Type, "docker"))
                {
                    // Default labels
                    var labels = new Dictionary<string, string>
                    {
                        [Constants.K8sEdgeModuleLabel] = KubeUtils.SanitizeLabelValue(moduleId.ModuleId),
                        [Constants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue(this.deviceId),
                        [Constants.K8sEdgeHubNameLabel] = KubeUtils.SanitizeLabelValue(this.iotHubHostname)
                    };

                    // Create a Service for every network interface of each module. (label them with hub, device and module id)
                    Option<V1Service> moduleService = this.GetServiceFromModule(labels, module, moduleId);
                    moduleService.ForEach(service => desiredServices.Add(service));

                    // Create a Pod for each module, and a proxy container.
                    V1PodTemplateSpec v1PodSpec = this.GetPodFromModule(labels, module, moduleId);

                    // if this is the edge agent's deployment then it needs to run under a specific service account
                    if (moduleIdentities[module.Name].ModuleId == CoreConstants.EdgeAgentModuleIdentityName)
                    {
                        v1PodSpec.Spec.ServiceAccountName = this.serviceAccountName;
                    }

                    // Bundle into a deployment
                    string deploymentName = this.DeploymentName(moduleIdentities[module.Name].ModuleId);
                    // Deployment data
                    var deploymentMeta = new V1ObjectMeta(name: deploymentName, labels: labels);

                    var selector = new V1LabelSelector(matchLabels: labels);
                    var deploymentSpec = new V1DeploymentSpec(replicas: 1, selector: selector, template: v1PodSpec);

                    desiredDeployments.Add(new V1Deployment(metadata: deploymentMeta, spec: deploymentSpec));

                    // Make the client call for the deployment
                    // V1Deployment deploymentResult = await this.client.CreateNamespacedDeploymentAsync(deployment, KubeUtils.K8sNamespace);
                    // What does the result tell us?
                }
                else
                {
                    KubernetesEventLogger<TConfig>.InvalidModuleType(module);
                }
            }

            // Find current Services/Deployments which need to be removed and updated
            var servicesRemoved = new List<V1Service>(currentServices.Items);
            servicesRemoved.RemoveAll(s => desiredServices.Exists(i => string.Equals(i.Metadata.Name, s.Metadata.Name)));
            var deploymentsRemoved = new List<V1Deployment>(currentDeployments.Items);
            deploymentsRemoved.RemoveAll(
                d =>
                {
                    return desiredDeployments.Exists(i => string.Equals(i.Metadata.Name, d.Metadata.Name)) ||
                           d.Metadata.Name == this.DeploymentName(CoreConstants.EdgeAgentModuleName);
                });

            var newServices = new List<V1Service>();
            desiredServices.ForEach(
                s =>
                {
                    string creationString = JsonConvert.SerializeObject(s);

                    if (currentV1ServicesFromAnnotations.ContainsKey(s.Metadata.Name))
                    {
                        string serviceAnnotation = currentV1ServicesFromAnnotations[s.Metadata.Name];
                        // If configuration matches, no need to update service
                        if (string.Equals(serviceAnnotation, creationString))
                        {
                            return;
                        }

                        if (s.Metadata.Annotations == null)
                        {
                            s.Metadata.Annotations = new Dictionary<string, string>();
                        }

                        s.Metadata.Annotations[Constants.CreationString] = creationString;

                        servicesRemoved.Add(s);
                        newServices.Add(s);
                        KubernetesEventLogger<TConfig>.UpdateService(s.Metadata.Name);
                    }
                    else
                    {
                        if (s.Metadata.Annotations == null)
                        {
                            s.Metadata.Annotations = new Dictionary<string, string>();
                        }

                        s.Metadata.Annotations[Constants.CreationString] = creationString;

                        newServices.Add(s);
                        KubernetesEventLogger<TConfig>.CreateService(s.Metadata.Name);
                    }
                });
            var deploymentsUpdated = new List<V1Deployment>();
            var newDeployments = new List<V1Deployment>();
            List<V1Deployment> currentDeploymentsList = currentDeployments.Items.ToList();
            desiredDeployments.ForEach(
                d =>
                {
                    if (currentDeploymentsFromAnnotations.ContainsKey(d.Metadata.Name))
                    {
                        V1Deployment current = currentDeploymentsList.Find(i => string.Equals(i.Metadata.Name, d.Metadata.Name));
                        string currentFromAnnotation = currentDeploymentsFromAnnotations[d.Metadata.Name];
                        string creationString = JsonConvert.SerializeObject(d);

                        // If configuration matches, or this is edgeAgent deployment and the images match,
                        // no need to do update deployment
                        if (string.Equals(currentFromAnnotation, creationString) ||
                            (string.Equals(d.Metadata.Name, this.DeploymentName(CoreConstants.EdgeAgentModuleName)) && V1DeploymentEx.ImageEquals(current, d)))
                        {
                            return;
                        }

                        d.Metadata.ResourceVersion = current.Metadata.ResourceVersion;
                        if (d.Metadata.Annotations == null)
                        {
                            var annotations = new Dictionary<string, string>
                            {
                                [Constants.CreationString] = creationString
                            };
                            d.Metadata.Annotations = annotations;
                        }
                        else
                        {
                            d.Metadata.Annotations[Constants.CreationString] = creationString;
                        }

                        deploymentsUpdated.Add(d);
                        KubernetesEventLogger<TConfig>.UpdateDeployment(d.Metadata.Name);
                    }
                    else
                    {
                        string creationString = JsonConvert.SerializeObject(d);
                        var annotations = new Dictionary<string, string>
                        {
                            [Constants.CreationString] = creationString
                        };
                        d.Metadata.Annotations = annotations;
                        newDeployments.Add(d);
                        KubernetesEventLogger<TConfig>.CreateDeployment(d.Metadata.Name);
                    }
                });

            // Remove the old
            IEnumerable<Task<V1Status>> removeServiceTasks = servicesRemoved.Select(
                i =>
                {
                    KubernetesEventLogger<TConfig>.DeletingService(i);
                    return this.client.DeleteNamespacedServiceAsync(i.Metadata.Name, KubeUtils.K8sNamespace, new V1DeleteOptions());
                });
            await Task.WhenAll(removeServiceTasks);

            IEnumerable<Task<V1Status>> removeDeploymentTasks = deploymentsRemoved.Select(
                d =>
                {
                    KubernetesEventLogger<TConfig>.DeletingDeployment(d);
                    return this.client.DeleteNamespacedDeployment1Async(d.Metadata.Name, KubeUtils.K8sNamespace, new V1DeleteOptions(propagationPolicy: "Foreground"), propagationPolicy: "Foreground");
                });
            await Task.WhenAll(removeDeploymentTasks);

            // Create the new.
            IEnumerable<Task<V1Service>> createServiceTasks = newServices.Select(
                s =>
                {
                    KubernetesEventLogger<TConfig>.CreatingService(s);
                    return this.client.CreateNamespacedServiceAsync(s, KubeUtils.K8sNamespace);
                });
            await Task.WhenAll(createServiceTasks);

            IEnumerable<Task<V1Deployment>> createDeploymentTasks = newDeployments.Select(
                deployment =>
                {
                    KubernetesEventLogger<TConfig>.CreatingDeployment(deployment);
                    return this.client.CreateNamespacedDeploymentAsync(deployment, KubeUtils.K8sNamespace);
                });
            await Task.WhenAll(createDeploymentTasks);

            // Update the existing - should only do this when different.
            //var updateServiceTasks = servicesUpdated.Select( s => this.client.ReplaceNamespacedServiceAsync(s, s.Metadata.Name, KubeUtils.K8sNamespace));
            //await Task.WhenAll(updateServiceTasks);
            IEnumerable<Task<V1Deployment>> updateDeploymentTasks = deploymentsUpdated.Select(deployment => this.client.ReplaceNamespacedDeploymentAsync(deployment, deployment.Metadata.Name, KubeUtils.K8sNamespace));
            await Task.WhenAll(updateDeploymentTasks);

            this.currentModules = desiredModules;
        }

        V1PodTemplateSpec GetPodFromModule(Dictionary<string, string> labels, KubernetesModule<TConfig> module, IModuleIdentity moduleIdentity)
        {
            if (module is IModule<AgentDocker.CombinedDockerConfig> moduleWithDockerConfig)
            {
                //pod labels
                var podLabels = new Dictionary<string, string>(labels);

                // pod annotations
                var podAnnotations = new Dictionary<string, string>();
                podAnnotations.Add(Constants.K8sEdgeOriginalModuleId, moduleIdentity.ModuleId);
                // Convert docker labels to annotations because docker labels don't have the same restrictions as
                // Kuberenetes labels.
                if (moduleWithDockerConfig.Config.CreateOptions?.Labels != null)
                {
                    foreach (KeyValuePair<string, string> label in moduleWithDockerConfig.Config.CreateOptions?.Labels)
                    {
                        podAnnotations.Add(KubeUtils.SanitizeAnnotationKey(label.Key), label.Value);
                    }
                }

                // Per container settings:
                // exposed ports
                Option<List<V1ContainerPort>> exposedPortsOption = (moduleWithDockerConfig.Config?.CreateOptions?.ExposedPorts != null)
                    ? this.GetExposedPorts(moduleWithDockerConfig.Config.CreateOptions.ExposedPorts).Map(
                        servicePorts =>
                            servicePorts.Select(tuple => new V1ContainerPort(tuple.Port, protocol: tuple.Protocol)).ToList())
                    : Option.None<List<V1ContainerPort>>();

                // privileged container
                Option<V1SecurityContext> securityContext = (moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Privileged == true) ? Option.Some(new V1SecurityContext(privileged: true)) : Option.None<V1SecurityContext>();

                // Environment Variables.
                List<V1EnvVar> env = this.CollectEnv(moduleWithDockerConfig, (KubernetesModuleIdentity)moduleIdentity);

                // Bind mounts
                (List<V1Volume> volumeList, List<V1VolumeMount> proxyMounts, List<V1VolumeMount> volumeMountList) = this.GetVolumesFromModule(moduleWithDockerConfig).GetOrElse((null, null, null));

                //Image
                string moduleImage = moduleWithDockerConfig.Config.Image;

                var containerList = new List<V1Container>()
                {
                    new V1Container(
                        KubeUtils.SanitizeDNSValue(moduleIdentity.ModuleId),
                        env: env,
                        image: moduleImage,
                        volumeMounts: volumeMountList,
                        securityContext: securityContext.GetOrElse(() => null),
                        ports: exposedPortsOption.GetOrElse(() => null)
                    ),

                    // TODO: Add Proxy container here - configmap for proxy configuration.
                    new V1Container(
                        "proxy",
                        env: env, // TODO: check these for validity for proxy.
                        image: this.proxyImage,
                        volumeMounts: proxyMounts)
                };

                Option<List<V1LocalObjectReference>> imageSecret = moduleWithDockerConfig.Config.AuthConfig.Map(
                    auth =>
                    {
                        var secret = new ImagePullSecret(auth);
                        var authList = new List<V1LocalObjectReference>
                        {
                            new V1LocalObjectReference(secret.Name)
                        };
                        return authList;
                    });

                var modulePodSpec = new V1PodSpec(containerList, volumes: volumeList, imagePullSecrets: imageSecret.GetOrElse(() => null));
                var objectMeta = new V1ObjectMeta(labels: podLabels, annotations: podAnnotations);
                return new V1PodTemplateSpec(metadata: objectMeta, spec: modulePodSpec);
            }
            else
            {
                KubernetesEventLogger<TConfig>.InvalidModuleType(module);
            }

            return new V1PodTemplateSpec();
        }

        private VolumeOptions GetVolumesFromModule(IModule<AgentDocker.CombinedDockerConfig> moduleWithDockerConfig)
        {
            var v1ConfigMapVolumeSource = new V1ConfigMapVolumeSource(null, null, this.proxyConfigVolumeName, null);

            var volumeList = new List<V1Volume>
            {
                new V1Volume(SocketVolumeName, emptyDir: new V1EmptyDirVolumeSource()),
                new V1Volume(ConfigVolumeName, configMap: v1ConfigMapVolumeSource)
            };
            var proxyMountList = new List<V1VolumeMount>
            {
                new V1VolumeMount(SocketDir, SocketVolumeName)
            };
            var volumeMountList = new List<V1VolumeMount>(proxyMountList);
            proxyMountList.Add(new V1VolumeMount(this.proxyConfigPath, ConfigVolumeName));

            if ((moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Binds == null) && (moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Mounts == null))
                return Option.Some((volumeList, proxyMountList, volumeMountList));

            if (moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Binds != null)
            {
                foreach (string bind in moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Binds)
                {
                    string[] bindSubstrings = bind.Split(':');
                    if (bindSubstrings.Count() >= 2)
                    {
                        string name = KubeUtils.SanitizeDNSValue(bindSubstrings[0]);
                        string type = "DirectoryOrCreate";
                        string hostPath = bindSubstrings[0];
                        string mountPath = bindSubstrings[1];
                        bool readOnly = ((bindSubstrings.Count() > 2) && bindSubstrings[2].Contains("ro"));
                        volumeList.Add(new V1Volume(name, hostPath: new V1HostPathVolumeSource(hostPath, type)));
                        volumeMountList.Add(new V1VolumeMount(mountPath, name, readOnlyProperty: readOnly));
                    }
                }
            }

            if (moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Mounts != null)
            {
                foreach (DockerModels.Mount mount in moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Mounts)
                {
                    if (mount.Type.Equals("bind", StringComparison.InvariantCultureIgnoreCase))
                    {
                        string name = KubeUtils.SanitizeDNSValue(mount.Source);
                        string type = "DirectoryOrCreate";
                        string hostPath = mount.Source;
                        string mountPath = mount.Target;
                        bool readOnly = mount.ReadOnly;
                        volumeList.Add(new V1Volume(name, hostPath: new V1HostPathVolumeSource(hostPath, type)));
                        volumeMountList.Add(new V1VolumeMount(mountPath, name, readOnlyProperty: readOnly));
                    }
                    else if (mount.Type.Equals("volume", StringComparison.InvariantCultureIgnoreCase))
                    {
                        string name = KubeUtils.SanitizeDNSValue(mount.Source);
                        string mountPath = mount.Target;
                        bool readOnly = mount.ReadOnly;
                        volumeList.Add(new V1Volume(name, emptyDir: new V1EmptyDirVolumeSource()));
                        volumeMountList.Add(new V1VolumeMount(mountPath, name, readOnlyProperty: readOnly));
                    }
                }
            }

            return volumeList.Count > 0 || volumeMountList.Count > 0
                ? Option.Some((volumeList, proxyMountList, volumeMountList))
                : Option.None<(List<V1Volume>, List<V1VolumeMount>, List<V1VolumeMount>)>();
        }

        private List<V1EnvVar> CollectEnv(IModule<AgentDocker.CombinedDockerConfig> moduleWithDockerConfig, KubernetesModuleIdentity identity)
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
            envList.Add(new V1EnvVar(CoreConstants.EdgeletModuleGenerationIdVariableName, identity.Credentials.ModuleGenerationId));
            envList.Add(new V1EnvVar(CoreConstants.DeviceIdVariableName, this.deviceId)); // could also get this from module identity
            envList.Add(new V1EnvVar(CoreConstants.ModuleIdVariableName, identity.ModuleId));
            envList.Add(new V1EnvVar(CoreConstants.EdgeletApiVersionVariableName, CoreConstants.EdgeletWorkloadApiVersion));

            if (string.Equals(identity.ModuleId, CoreConstants.EdgeAgentModuleIdentityName))
            {
                envList.Add(new V1EnvVar(CoreConstants.ModeKey, CoreConstants.KubernetesMode));
                envList.Add(new V1EnvVar(CoreConstants.EdgeletManagementUriVariableName, this.managementUri.ToString()));
                envList.Add(new V1EnvVar(CoreConstants.NetworkIdKey, "azure-iot-edge"));
                envList.Add(new V1EnvVar(CoreConstants.ProxyImageEnvKey, this.proxyImage));
                envList.Add(new V1EnvVar(CoreConstants.ProxyConfigPathEnvKey, this.proxyConfigPath));
                envList.Add(new V1EnvVar(CoreConstants.ProxyConfigVolumeEnvKey, this.proxyConfigVolumeName));
                envList.Add(new V1EnvVar(CoreConstants.EdgeAgentServiceAccountName, this.serviceAccountName));
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

        private Option<V1Service> GetServiceFromModule(Dictionary<string, string> labels, KubernetesModule<TConfig> module, IModuleIdentity moduleIdentity)
        {
            var portList = new List<V1ServicePort>();
            Option<Dictionary<string, string>> serviceAnnotations = Option.None<Dictionary<string, string>>();
            bool onlyExposedPorts = true;
            if (module is IModule<AgentDocker.CombinedDockerConfig> moduleWithDockerConfig)
            {
                if (moduleWithDockerConfig.Config.CreateOptions?.Labels != null)
                {
                    // Add annotations from Docker labels. This provides the customer a way to assign annotations to services if they want
                    // to tie backend services to load balancers via an Ingress Controller.
                    var annotations = new Dictionary<string, string>();
                    foreach (KeyValuePair<string, string> label in moduleWithDockerConfig.Config.CreateOptions?.Labels)
                    {
                        annotations.Add(KubeUtils.SanitizeAnnotationKey(label.Key), label.Value);
                    }

                    serviceAnnotations = Option.Some(annotations);
                }

                // Handle ExposedPorts entries
                if (moduleWithDockerConfig.Config?.CreateOptions?.ExposedPorts != null)
                {
                    // Entries in the Exposed Port list just tell Docker that this container wants to listen on that port.
                    // We interpret this as a "ClusterIP" service type
                    this.GetExposedPorts(moduleWithDockerConfig.Config.CreateOptions.ExposedPorts)
                        .ForEach(
                            exposedList =>
                                exposedList.ForEach((item) => portList.Add(new V1ServicePort(item.Port, name: $"{item.Port}-{item.Protocol.ToLower()}", protocol: item.Protocol))));
                }

                // Handle HostConfig PortBindings entries
                if (moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.PortBindings != null)
                {
                    foreach (KeyValuePair<string, IList<DockerModels.PortBinding>> portBinding in moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.PortBindings)
                    {
                        string[] portProtocol = portBinding.Key.Split('/');
                        if (portProtocol.Length == 2)
                        {
                            int port;
                            string protocol;
                            if (int.TryParse(portProtocol[0], out port) && this.ValidateProtocol(portProtocol[1], out protocol))
                            {
                                // Entries in Docker portMap wants to expose a port on the host (hostPort) and map it to the container's port (port)
                                // We interpret that as the pod wants the cluster to expose a port on a public IP (hostPort), and target it to the container's port (port)
                                foreach (DockerModels.PortBinding hostBinding in portBinding.Value)
                                {
                                    int hostPort;
                                    if (int.TryParse(hostBinding.HostPort, out hostPort))
                                    {
                                        portList.Add(new V1ServicePort(hostPort, name: $"{port}-{protocol.ToLower()}", protocol: protocol, targetPort: port));
                                        onlyExposedPorts = false;
                                    }
                                    else
                                    {
                                        KubernetesEventLogger<TConfig>.PortBindingValue(module, portBinding.Key);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (portList.Count > 0)
            {
                // Selector: by module name and device name, also how we will label this puppy.
                var objectMeta = new V1ObjectMeta(annotations: serviceAnnotations.GetOrElse(() => null), labels: labels, name: KubeUtils.SanitizeDNSValue(moduleIdentity.ModuleId));
                // How we manage this service is dependent on the port mappings user asks for.
                // If the user tells us to only use ClusterIP ports, we will always set the type to ClusterIP.
                // If all we had were exposed ports, we will assume ClusterIP.
                // If we have mapped ports, we are going to use "LoadBalancer" service Type - to tell the ingress controller we want this port exposed.
                //
                // If the user wants to expose the ClusterIPs port externally, they should manually create a service to expose it.
                // This gives the user more control as to how they want this to work.
                string serviceType;
                if (onlyExposedPorts)
                {
                    serviceType = "ClusterIP";
                }
                else
                {
                    serviceType = this.defaultMapServiceType;
                }

                return Option.Some(new V1Service(metadata: objectMeta, spec: new V1ServiceSpec(type: serviceType, ports: portList, selector: labels)));
            }
            else
            {
                return Option.None<V1Service>();
            }
        }

        private Option<List<(int Port, string Protocol)>> GetExposedPorts(IDictionary<string, DockerModels.EmptyStruct> exposedPorts)
        {
            var serviceList = new List<(int, string)>();
            foreach (KeyValuePair<string, DockerModels.EmptyStruct> exposedPort in exposedPorts)
            {
                string[] portProtocol = exposedPort.Key.Split('/');
                if (portProtocol.Length == 2)
                {
                    if (int.TryParse(portProtocol[0], out int port) && this.ValidateProtocol(portProtocol[1], out string protocol))
                    {
                        serviceList.Add((port, protocol));
                    }
                    else
                    {
                        KubernetesEventLogger<TConfig>.ExposedPortValue(exposedPort.Key);
                    }
                }
            }

            return (serviceList.Count > 0) ? Option.Some(serviceList) : Option.None<List<(int, string)>>();
        }

        private bool ValidateProtocol(string dockerProtocol, out string k8SProtocol)
        {
            bool result = true;
            switch (dockerProtocol.ToUpper())
            {
                case "TCP":
                    k8SProtocol = "TCP";
                    break;
                case "UDP":
                    k8SProtocol = "UDP";
                    break;
                case "SCTP":
                    k8SProtocol = "SCTP";
                    break;
                default:
                    k8SProtocol = "TCP";
                    result = false;
                    break;
            }

            return result;
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

                    KubernetesEventLogger<TConfig>.InvalidCreationString("service", "null service");
                    throw new NullReferenceException("null service in list");
                },
                service =>
                {
                    if (service != null)
                    {
                        if (service.Metadata?.Annotations != null)
                        {
                            if (service.Metadata.Annotations.TryGetValue(Constants.CreationString, out string creationString))
                            {
                                return creationString;
                            }
                        }

                        KubernetesEventLogger<TConfig>.InvalidCreationString(service.Kind, service.Metadata?.Name);

                        var serviceWithoutStatus = new V1Service(service.ApiVersion, service.Kind, service.Metadata, service.Spec);
                        return JsonConvert.SerializeObject(serviceWithoutStatus);
                    }

                    KubernetesEventLogger<TConfig>.InvalidCreationString("service", "null service");
                    throw new NullReferenceException("null service in list");
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

                    KubernetesEventLogger<TConfig>.InvalidCreationString("deployment", "null deployment");
                    throw new NullReferenceException("null deployment in list");
                },
                deployment =>
                {
                    if (deployment != null)
                    {
                        if (deployment.Metadata?.Annotations != null)
                        {
                            if (deployment.Metadata.Annotations.TryGetValue(Constants.CreationString, out string creationString))
                            {
                                return creationString;
                            }
                        }

                        KubernetesEventLogger<TConfig>.InvalidCreationString(deployment.Kind, deployment.Metadata?.Name);
                        var deploymentWithoutStatus = new V1Deployment(deployment.ApiVersion, deployment.Kind, deployment.Metadata, deployment.Spec);
                        return JsonConvert.SerializeObject(deploymentWithoutStatus);
                    }

                    KubernetesEventLogger<TConfig>.InvalidCreationString("deployment", "null deployment");
                    throw new NullReferenceException("null deployment in list");
                });
        }

        // TODO : BEGIN POD WATCH
        private async Task ListPodComplete(Task<HttpOperationResponse<V1PodList>> podListRespTask)
        {
            if (podListRespTask != null)
            {
                HttpOperationResponse<V1PodList> podListResp = await podListRespTask;
                if (podListResp != null)
                {
                    this.podWatch = Option.Some(
                        podListResp.Watch<V1Pod>(
                            onEvent: async (type, item) =>
                            {
                                try
                                {
                                    await this.WatchPodEventsAsync(type, item);
                                }
                                catch (Exception ex) when (!ex.IsFatal())
                                {
                                    KubernetesEventLogger<TConfig>.ExceptionInPodWatch(ex);
                                }
                            },
                            onClosed: () =>
                            {
                                KubernetesEventLogger<TConfig>.PodWatchClosed();

                                // get rid of the current pod watch object since we got closed
                                this.podWatch.ForEach(watch => watch.Dispose());
                                this.podWatch = Option.None<Watcher<V1Pod>>();

                                // kick off a new watch
                                this.client.ListNamespacedPodWithHttpMessagesAsync(KubeUtils.K8sNamespace, watch: true).ContinueWith(this.ListPodComplete);
                            },
                            onError: KubernetesEventLogger<TConfig>.ExceptionInPodWatch
                        ));
                }
                else
                {
                    KubernetesEventLogger<TConfig>.NullListResponse("ListNamespacedPodWithHttpMessagesAsync", "http response");
                    throw new NullReferenceException("Null response from ListNamespacedPodWithHttpMessagesAsync");
                }
            }
            else
            {
                KubernetesEventLogger<TConfig>.NullListResponse("ListNamespacedPodWithHttpMessagesAsync", "task");
                throw new NullReferenceException("Null Task from ListNamespacedPodWithHttpMessagesAsync");
            }
        }

        private async Task WatchPodEventsAsync(WatchEventType type, V1Pod item)
        {
            // if the pod doesn't have the module label set then we are not interested in it
            if (item.Metadata.Labels.ContainsKey(Constants.K8sEdgeModuleLabel) == false)
            {
                return;
            }

            string podName = item.Metadata.Labels[Constants.K8sEdgeModuleLabel];
            KubernetesEventLogger<TConfig>.PodStatus(type, podName);
            switch (type)
            {
                case WatchEventType.Added:
                case WatchEventType.Modified:
                case WatchEventType.Error:
                    ModuleRuntimeInfo runtimeInfo = this.ConvertPodToRuntime(podName, item);
                    using (await this.moduleLock.LockAsync())
                    {
                        this.moduleRuntimeInfos[podName] = runtimeInfo;
                    }

                    break;
                case WatchEventType.Deleted:
                    using (await this.moduleLock.LockAsync())
                    {
                        ModuleRuntimeInfo removedRuntimeInfo;
                        if (!this.moduleRuntimeInfos.TryRemove(podName, out removedRuntimeInfo))
                        {
                            KubernetesEventLogger<TConfig>.PodStatusRemoveError(podName);
                        }
                    }

                    break;
            }
        }

        private ModuleRuntimeInfo ConvertPodToRuntime(string name, V1Pod pod)
        {
            string moduleName = name;
            pod.Metadata?.Annotations?.TryGetValue(Constants.K8sEdgeOriginalModuleId, out moduleName);

            Option<V1ContainerStatus> containerStatus = this.GetContainerByName(name, pod);
            (ModuleStatus moduleStatus, string statusDescription) = this.ConvertPodStatusToModuleStatus(containerStatus);
            (int exitCode, Option<DateTime> startTime, Option<DateTime> exitTime, string imageHash) = this.GetRuntimedata(containerStatus.OrDefault());

            var reportedConfig = new AgentDocker.DockerReportedConfig(string.Empty, string.Empty, imageHash);
            return new ModuleRuntimeInfo<AgentDocker.DockerReportedConfig>(
                ModuleIdentityHelper.GetModuleName(moduleName),
                "docker",
                moduleStatus,
                statusDescription,
                exitCode,
                startTime,
                exitTime,
                reportedConfig);
        }

        private Option<V1ContainerStatus> GetContainerByName(string name, V1Pod pod)
        {
            string containerName = KubeUtils.SanitizeDNSValue(name);
            if (pod.Status?.ContainerStatuses != null)
            {
                foreach (var status in pod.Status.ContainerStatuses)
                {
                    if (string.Equals(status.Name, containerName, StringComparison.OrdinalIgnoreCase))
                    {
                        return Option.Some(status);
                    }
                }
            }

            return Option.None<V1ContainerStatus>();
        }

        private (ModuleStatus, string) ConvertPodStatusToModuleStatus(Option<V1ContainerStatus> podStatus)
        {
            // TODO: Possibly refine this?
            return podStatus.Map(
                pod =>
                {
                    if (pod.State.Running != null)
                    {
                        return (ModuleStatus.Running, $"Started at {pod.State.Running.StartedAt.GetValueOrDefault(DateTime.Now)}");
                    }
                    else if (pod.State.Terminated != null)
                    {
                        return (ModuleStatus.Failed, pod.State.Terminated.Message);
                    }
                    else if (pod.State.Waiting != null)
                    {
                        return (ModuleStatus.Failed, pod.State.Waiting.Message);
                    }

                    return (ModuleStatus.Unknown, "Unknown");
                }).GetOrElse(() => (ModuleStatus.Unknown, "Unknown"));
        }

        private (int, Option<DateTime>, Option<DateTime>, string image) GetRuntimedata(V1ContainerStatus status)
        {
            if (status?.LastState?.Running != null)
            {
                if (status.LastState.Running.StartedAt.HasValue)
                {
                    return (0, Option.Some(status.LastState.Running.StartedAt.Value), Option.None<DateTime>(), status.Image);
                }
            }
            else
            {
                if (status?.LastState?.Terminated?.StartedAt != null &&
                    status.LastState.Terminated.FinishedAt.HasValue)
                {
                    return (0, Option.Some(status.LastState.Terminated.StartedAt.Value), Option.Some(status.LastState.Terminated.FinishedAt.Value), status.Image);
                }
            }

            return (0, Option.None<DateTime>(), Option.None<DateTime>(), String.Empty);
        }

        // TODO : END POD WATCH
    }
}
