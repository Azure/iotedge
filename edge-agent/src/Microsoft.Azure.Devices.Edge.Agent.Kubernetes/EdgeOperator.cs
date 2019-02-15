// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
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

    public class EdgeOperator : IKubernetesOperator, IRuntimeInfoProvider
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
        readonly TypeSpecificSerDe<EdgeDeploymentDefinition> deploymentSerde;
        readonly JsonSerializerSettings crdSerializerSettings;
        readonly AsyncLock watchLock;

        const string LOWER_ASCII = "abcdefghijklmnopqrstuvwxyz";
        const string ALLOWED_CHARS_LABEL_VALUES = "-._";
        const string ALLOWED_CHARS_DNS = "-";
        const string ALLOWED_CHARS_GENERIC = "-.";
        const int MAX_K8S_VALUE_LENGTH = 253;
        const int MAX_DNS_NAME_LENGTH = 63;
        const int MAX_LABEL_VALUE_LENGTH = 63;

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
            IKubernetes client)
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
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.moduleRuntimeInfos = new Dictionary<string, ModuleRuntimeInfo>();
            this.moduleLock = new AsyncLock();
            this.watchLock = new AsyncLock();
            this.podWatch = Option.None<Watcher<V1Pod>>();
            this.resourceName = this.iotHubHostname + Constants.k8sNameDivider + this.deviceId;
            this.deploymentSelector = Constants.k8sEdgeDeviceLabel + " = " + this.deviceId + "," + Constants.k8sEdgeHubNameLabel + "=" + this.iotHubHostname;
            var deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>
            {
                [typeof(IModule)] = new Dictionary<string, Type>
                {
                    ["docker"] = typeof(CombinedDockerModule)
                }
            };

            this.deploymentSerde = new TypeSpecificSerDe<EdgeDeploymentDefinition>(deserializerTypesMap, new CamelCasePropertyNamesContractResolver());
            this.crdSerializerSettings = new JsonSerializerSettings
            {

                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
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

        public async Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken ctsToken)
        {
            using (await this.moduleLock.LockAsync())
            {
                return this.moduleRuntimeInfos.Select(kvp => kvp.Value);
            }
        }

        public async Task<SystemInfo> GetSystemInfo()
        {
            V1NodeList k8SNodes = await this.client.ListNodeAsync();
            string osType = string.Empty;
            string arch = string.Empty;
            string version = string.Empty;
            V1Node firstNode = k8SNodes.Items.FirstOrDefault();
            if (firstNode != null)
            {
                osType = firstNode.Status.NodeInfo.OperatingSystem;
                arch = firstNode.Status.NodeInfo.Architecture;
                version = firstNode.Status.NodeInfo.OsImage;
            }
            return new SystemInfo(osType, arch, version);
        }

        private async Task ListCrdComplete(Task<HttpOperationResponse<object>> customObjectWatchTask)
        {
            if (customObjectWatchTask != null)
            {
                HttpOperationResponse<object> customObjectWatch = await customObjectWatchTask;
                if (customObjectWatch != null)
                {
                    // We can add events to a watch once created, like if connection is closed, etc.
                    this.operatorWatch = Option.Some(customObjectWatch.Watch<object>(
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
                            this.operatorWatch = Option.None<Watcher<object>>();

                            // kick off a new watch
                            this.client.ListClusterCustomObjectWithHttpMessagesAsync(
                                Constants.k8sCrdGroup,
                                Constants.k8sApiVersion,
                                Constants.k8sCrdPlural,
                                watch: true).ContinueWith(ListCrdComplete);
                        },
                        onError: ex =>
                        {
                            Events.ExceptionInCustomResourceWatch(ex);
                        }
                    ));
                }
                else
                {
                    Events.NullListResponse("ListClusterCustomObjectWithHttpMessagesAsync", "http response");
                    throw new NullReferenceException("Null response from ListClusterCustomObjectWithHttpMessagesAsync");
                }
            }
            else
            {
                Events.NullListResponse("ListClusterCustomObjectWithHttpMessagesAsync", "task");
                throw new NullReferenceException("Null Task from ListClusterCustomObjectWithHttpMessagesAsync");
            }
        }

        private async Task ListPodComplete(Task<HttpOperationResponse<V1PodList>> podListRespTask)
        {
            if (podListRespTask != null)
            {
                HttpOperationResponse<V1PodList> podListResp = await podListRespTask;
                if (podListResp != null)
                {
                    this.podWatch = Option.Some(podListResp.Watch<V1Pod>(
                            onEvent: async (type, item) =>
                            {
                                try
                                {
                                    await this.WatchPodEventsAsync(type, item);
                                }
                                catch (Exception ex) when (!ex.IsFatal())
                                {
                                    Events.ExceptionInPodWatch(ex);
                                }
                            },
                            onClosed: () =>
                            {
                                Events.PodWatchClosed();

                                // get rid of the current pod watch object since we got closed
                                this.podWatch.ForEach(watch => watch.Dispose());
                                this.podWatch = Option.None<Watcher<V1Pod>>();

                                // kick off a new watch
                                this.client.ListNamespacedPodWithHttpMessagesAsync(Constants.k8sNamespace, watch: true).ContinueWith(this.ListPodComplete);
                            },
                            onError: ex =>
                            {
                                Events.ExceptionInPodWatch(ex);
                            }
                        ));
                }
                else
                {
                    Events.NullListResponse("ListNamespacedPodWithHttpMessagesAsync", "http response");
                    throw new NullReferenceException("Null response from ListNamespacedPodWithHttpMessagesAsync");
                }
            }
            else
            {
                Events.NullListResponse("ListNamespacedPodWithHttpMessagesAsync", "task");
                throw new NullReferenceException("Null Task from ListNamespacedPodWithHttpMessagesAsync");
            }
        }

        public void Start()
        {
            // The following "List..." requests do not return until there is something to return, so if we "await" here,
            // there is a chance that one or both of these requests will block forever - we won't start creating these pods and CRDs
            // until we receive a deployment.
            // Considering setting up these watches is critical to the operation of EdgeAgent, throwing an exception and letting the process crash
            // is an acceptable fate if these tasks fail.

            // Pod watching for module runtime status.
            this.client.ListNamespacedPodWithHttpMessagesAsync(Constants.k8sNamespace, watch: true).ContinueWith(this.ListPodComplete);

            // CRD watch
            this.client.ListClusterCustomObjectWithHttpMessagesAsync(Constants.k8sCrdGroup, Constants.k8sApiVersion, Constants.k8sCrdPlural, watch: true).ContinueWith(this.ListCrdComplete);
        }

        Option<List<(int Port, string Protocol)>> GetExposedPorts(IDictionary<string, DockerModels.EmptyStruct> exposedPorts)
        {
            var serviceList = new List<(int, string)>();
            foreach (KeyValuePair<string, DockerModels.EmptyStruct> exposedPort in exposedPorts)
            {
                string[] portProtocol = exposedPort.Key.Split('/');
                if (portProtocol.Length == 2)
                {
                    int port;
                    string protocol;
                    if (int.TryParse(portProtocol[0], out port) && this.ValidateProtocol(portProtocol[1], out protocol))
                    {
                        serviceList.Add((port, protocol));
                    }
                    else
                    {
                        Events.ExposedPortValue(exposedPort.Key);
                    }
                }
            }
            return (serviceList.Count > 0) ? Option.Some(serviceList) : Option.None<List<(int, string)>>();
        }

        Option<V1Service> GetServiceFromModule(Dictionary<string, string> labels, KubernetesModule module)
        {
            var portList = new List<V1ServicePort>();
            bool onlyExposedPorts = true;
            if (module.Module is IModule<AgentDocker.CombinedDockerConfig> moduleWithDockerConfig)
            {
                // Handle ExposedPorts entries
                if (moduleWithDockerConfig.Config?.CreateOptions?.ExposedPorts != null)
                {
                    this.GetExposedPorts(moduleWithDockerConfig.Config.CreateOptions.ExposedPorts)
                        .ForEach(exposedList =>
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
                                // k8s requires HostPort to be above 30000, and I am ignoring HostIP
                                foreach (DockerModels.PortBinding hostBinding in portBinding.Value)
                                {
                                    int hostPort;
                                    if (int.TryParse(hostBinding.HostPort, out hostPort))
                                    {
                                        portList.Add(new V1ServicePort(port, name: $"{port}-{protocol.ToLower()}", protocol: protocol, targetPort: hostPort));
                                        onlyExposedPorts = false;
                                    }
                                    else
                                    {
                                        Events.PortBindingValue(module, portBinding.Key);
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
                var objectMeta = new V1ObjectMeta(labels: labels, name: SanitizeDNSValue(module.ModuleIdentity.ModuleId));
                // How we manage this service is dependent on the port mappings user asks for.
                string serviceType;
                if (onlyExposedPorts)
                {
                    serviceType = "ClusterIP";
                }
                else
                {
                    serviceType = "NodePort";
                }
                return Option.Some(new V1Service(metadata: objectMeta, spec: new V1ServiceSpec(type: serviceType, ports: portList, selector: labels)));
            }
            else
            {
                return Option.None<V1Service>();
            }
        }

        bool ValidateProtocol(string dockerProtocol, out string k8SProtocol)
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

        async Task WatchDeploymentEventsAsync(WatchEventType type, object custom)
        {
            using (await this.watchLock.LockAsync())
            {
                EdgeDeploymentDefinition customObject;
                try
                {
                    string customString = JsonConvert.SerializeObject(custom);
                    customObject = this.deploymentSerde.Deserialize(customString);
                }
                catch (Exception e)
                {
                    Events.EdgeDeploymentDeserializeFail(e);
                    return;
                }

                // only operate on the device that matches this operator.
                if (String.Equals(customObject.Metadata.Name, this.resourceName, StringComparison.OrdinalIgnoreCase))
                {
                    V1ServiceList currentServices = await this.client.ListNamespacedServiceAsync(Constants.k8sNamespace, labelSelector: this.deploymentSelector);
                    V1DeploymentList currentDeployments = await this.client.ListNamespacedDeploymentAsync(Constants.k8sNamespace, labelSelector: this.deploymentSelector);
                    Events.DeploymentStatus(type, this.resourceName);
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
                                IEnumerable<Task<V1Status>> removeServiceTasks = currentServices.Items.Select(i => this.client.DeleteNamespacedServiceAsync(new V1DeleteOptions(), i.Metadata.Name, Constants.k8sNamespace));
                                await Task.WhenAll(removeServiceTasks);
                                IEnumerable<Task<V1Status>> removeDeploymentTasks = currentDeployments.Items.Select(
                                    d => this.client.DeleteNamespacedDeployment1Async(
                                        new V1DeleteOptions(propagationPolicy: "Foreground"), d.Metadata.Name, Constants.k8sNamespace, propagationPolicy: "Foreground"));
                                await Task.WhenAll(removeDeploymentTasks);
                            }
                            break;
                        case WatchEventType.Error:
                            Events.DeploymentError();
                            break;
                    }
                }
                else
                {
                    Events.DeploymentNameMismatch(customObject.Metadata.Name, this.resourceName);
                }
            }
        }

        Dictionary<string,string> GetCurrentServiceConfig(V1ServiceList currentServices)
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
                    if (service != null)
                    {
                        if (service.Metadata?.Annotations != null)
                        {
                            if (service.Metadata.Annotations.TryGetValue(Constants.CreationString, out string creationString))
                            {
                                return creationString;
                            }
                        }
                        Events.InvalidCreationString(service.Kind, service.Metadata?.Name);

                        var serviceWithoutStatus = new V1Service(service.ApiVersion, service.Kind, service.Metadata, service.Spec);
                        return JsonConvert.SerializeObject(serviceWithoutStatus);
                    }

                    Events.InvalidCreationString("service", "null service");
                    throw new NullReferenceException("null service in list");
                });
        }

        Dictionary<string, string> GetCurrentDeploymentConfig(V1DeploymentList currentDeployments)
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
                    if (deployment != null)
                    {
                        if (deployment.Metadata?.Annotations != null)
                        {
                            if (deployment.Metadata.Annotations.TryGetValue(Constants.CreationString, out string creationString))
                            {
                                return creationString;
                            }
                        }
                        Events.InvalidCreationString(deployment.Kind, deployment.Metadata?.Name);
                        var deploymentWithoutStatus = new V1Deployment(deployment.ApiVersion, deployment.Kind, deployment.Metadata, deployment.Spec);
                        return JsonConvert.SerializeObject(deploymentWithoutStatus);
                    }

                    Events.InvalidCreationString("deployment", "null deployment");
                    throw new NullReferenceException("null deployment in list");
                });
        }

        string DeploymentName(string moduleId) => SanitizeK8sValue(this.iotHubHostname + "-" + this.deviceId + "-" + moduleId);

        async void ManageDeployments(V1ServiceList currentServices, V1DeploymentList currentDeployments, EdgeDeploymentDefinition customObject)
        {
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
            foreach (KubernetesModule module in customObject.Spec)
            {
                if (string.Equals(module.Module.Type, "docker"))
                {
                    // Do not try to update edgeAgent -
                    // this is a stopgap measure so that current deployments don't accidentally overwrite the custom edgeAgent needed for k8s
                    // TODO: remove this check.
                    if (module.ModuleIdentity.ModuleId == CoreConstants.EdgeAgentModuleIdentityName)
                    {
                        continue;
                    }

                    // Default labels
                    var labels = new Dictionary<string, string>
                    {
                        [Constants.k8sEdgeModuleLabel] = SanitizeLabelValue(module.ModuleIdentity.ModuleId),
                        [Constants.k8sEdgeDeviceLabel] = SanitizeLabelValue(this.deviceId),
                        [Constants.k8sEdgeHubNameLabel] = SanitizeLabelValue(this.iotHubHostname)
                    };

                    // Create a Service for every network interface of each module. (label them with hub, device and module id)
                    Option<V1Service> moduleService = this.GetServiceFromModule(labels, module);
                    moduleService.ForEach(service => desiredServices.Add(service));

                    // Create a Pod for each module, and a proxy container.
                    V1PodTemplateSpec v1PodSpec = this.GetPodFromModule(labels, module);

                    // if this is the edge agent's deployment then it needs to run under a specific service account
                    if (module.ModuleIdentity.ModuleId == CoreConstants.EdgeAgentModuleIdentityName)
                    {
                        v1PodSpec.Spec.ServiceAccountName = this.serviceAccountName;
                    }

                    // Bundle into a deployment
                    string deploymentName = this.DeploymentName(module.ModuleIdentity.ModuleId);
                    // Deployment data
                    var deploymentMeta = new V1ObjectMeta(name: deploymentName, labels: labels);

                    var selector = new V1LabelSelector(matchLabels: labels);
                    var deploymentSpec = new V1DeploymentSpec(replicas: 1, selector: selector, template: v1PodSpec);

                    desiredDeployments.Add(new V1Deployment(metadata: deploymentMeta, spec: deploymentSpec));

                    // Make the client call for the deployment
                    // V1Deployment deploymentResult = await this.client.CreateNamespacedDeploymentAsync(deployment, Constants.k8sNamespace);
                    // What does the result tell us?
                }
                else
                {
                    Events.InvalidModuleType(module);
                }
            }

            // Find current Services/Deployments which need to be removed and updated
            var servicesRemoved = new List<V1Service>(currentServices.Items);
            servicesRemoved.RemoveAll(s => desiredServices.Exists(i => string.Equals(i.Metadata.Name, s.Metadata.Name)));
            var deploymentsRemoved = new List<V1Deployment>(currentDeployments.Items);
            deploymentsRemoved.RemoveAll(d =>
            {
                return desiredDeployments.Exists(i => string.Equals(i.Metadata.Name, d.Metadata.Name)) ||
                    d.Metadata.Name == this.DeploymentName(CoreConstants.EdgeAgentModuleName);
            });

            var newServices = new List<V1Service>();
            desiredServices.ForEach(
                s =>
                {
                    if (currentV1ServicesFromAnnotations.ContainsKey(s.Metadata.Name))
                    {
                        string serviceAnnotation = currentV1ServicesFromAnnotations[s.Metadata.Name];
                        string creationString = JsonConvert.SerializeObject(s);
                        // If configuration matches, no need to update service
                        if (string.Equals(serviceAnnotation, creationString))
                            return;
                        if (s.Metadata.Annotations == null)
                        {
                            var annotations = new Dictionary<string, string>
                            {
                                [Constants.CreationString] = creationString
                            };
                            s.Metadata.Annotations = annotations;
                        }
                        else
                        {
                            s.Metadata.Annotations[Constants.CreationString] = creationString;
                        }

                        servicesRemoved.Add(s);
                        newServices.Add(s);
                        Events.UpdateService(s.Metadata.Name);
                    }
                    else
                    {
                        string creationString = JsonConvert.SerializeObject(s);
                        var annotations = new Dictionary<string, string>
                        {
                            [Constants.CreationString] = creationString
                        };
                        s.Metadata.Annotations = annotations;
                        newServices.Add(s);
                        Events.CreateService(s.Metadata.Name);
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
                        if (string.Equals(currentFromAnnotation,creationString) ||
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
                        Events.UpdateDeployment(d.Metadata.Name);
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
                        Events.CreateDeployment(d.Metadata.Name);
                    }
                });

            // Remove the old
            IEnumerable<Task<V1Status>> removeServiceTasks = servicesRemoved.Select(i =>
            {
                Events.DeletingService(i);
                return this.client.DeleteNamespacedServiceAsync(new V1DeleteOptions(), i.Metadata.Name, Constants.k8sNamespace);
            });
            await Task.WhenAll(removeServiceTasks);

            IEnumerable<Task<V1Status>> removeDeploymentTasks = deploymentsRemoved.Select(d =>
            {
                Events.DeletingDeployment(d);
                return this.client.DeleteNamespacedDeployment1Async(new V1DeleteOptions(propagationPolicy: "Foreground"), d.Metadata.Name, Constants.k8sNamespace, propagationPolicy: "Foreground");
            });
            await Task.WhenAll(removeDeploymentTasks);

            // Create the new.
            IEnumerable<Task<V1Service>> createServiceTasks = newServices.Select(s =>
            {
                Events.CreatingService(s);
                return this.client.CreateNamespacedServiceAsync(s, Constants.k8sNamespace);
            });
            await Task.WhenAll(createServiceTasks);

            IEnumerable<Task<V1Deployment>> createDeploymentTasks = newDeployments.Select(deployment =>
            {
                Events.CreatingDeployment(deployment);
                return this.client.CreateNamespacedDeploymentAsync(deployment, Constants.k8sNamespace);
            });
            await Task.WhenAll(createDeploymentTasks);

            // Update the existing - should only do this when different.
            //var updateServiceTasks = servicesUpdated.Select( s => this.client.ReplaceNamespacedServiceAsync(s, s.Metadata.Name, Constants.k8sNamespace));
            //await Task.WhenAll(updateServiceTasks);
            IEnumerable<Task<V1Deployment>> updateDeploymentTasks = deploymentsUpdated.Select(deployment => this.client.ReplaceNamespacedDeploymentAsync(deployment, deployment.Metadata.Name, Constants.k8sNamespace));
            await Task.WhenAll(updateDeploymentTasks);
        }

        V1PodTemplateSpec GetPodFromModule(Dictionary<string, string> labels, KubernetesModule module)
        {
            if (module.Module is IModule<AgentDocker.CombinedDockerConfig> moduleWithDockerConfig)
            {
                //pod labels
                var podLabels = new Dictionary<string, string>(labels);
                if (moduleWithDockerConfig.Config.CreateOptions?.Labels != null)
                {
                    foreach (KeyValuePair<string, string> label in moduleWithDockerConfig.Config.CreateOptions?.Labels)
                    {
                        podLabels.Add(label.Key, label.Value);
                    }
                }

                // pod annotations
                var podAnnotations = new Dictionary<string, string>();
                podAnnotations.Add(Constants.k8sEdgeOriginalModuleId, module.ModuleIdentity.ModuleId);

                // Per container settings:
                // exposed ports
                Option<List<V1ContainerPort>> exposedPortsOption = (moduleWithDockerConfig.Config?.CreateOptions?.ExposedPorts != null) ?
                    this.GetExposedPorts(moduleWithDockerConfig.Config.CreateOptions.ExposedPorts).Map(servicePorts =>
                       servicePorts.Select(tuple => new V1ContainerPort(tuple.Port, protocol: tuple.Protocol)).ToList()) :
                    Option.None<List<V1ContainerPort>>();

                // privileged container
                Option<V1SecurityContext> securityContext = (moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Privileged == true) ?
                    Option.Some(new V1SecurityContext(privileged: true)) :
                    Option.None<V1SecurityContext>();

                // Environment Variables.
                List<V1EnvVar> env = this.CollectEnv(moduleWithDockerConfig, module.ModuleIdentity);

                // Bind mounts
                (List<V1Volume> volumeList, List<V1VolumeMount> proxyMounts, List<V1VolumeMount> volumeMountList) = this.GetVolumesFromModule(moduleWithDockerConfig).GetOrElse((null, null, null));

                //Image
                string moduleImage = moduleWithDockerConfig.Config.Image;

                var containerList = new List<V1Container>()
                {
                    new V1Container(SanitizeDNSValue(module.ModuleIdentity.ModuleId),
                        env: env,
                        image: moduleImage,
                        volumeMounts: volumeMountList,
                        securityContext: securityContext.GetOrElse(() => null),
                        ports: exposedPortsOption.GetOrElse(() => null)
                    ),

                    // TODO: Add Proxy container here - configmap for proxy configuration.
                    new V1Container("proxy",
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
                Events.InvalidModuleType(module);
            }
            return new V1PodTemplateSpec();
        }

        VolumeOptions GetVolumesFromModule(IModule<AgentDocker.CombinedDockerConfig> moduleWithDockerConfig)
        {
            var v1ConfigMapVolumeSource = new V1ConfigMapVolumeSource(null, null, this.proxyConfigVolumeName, null);

            var volumeList = new List<V1Volume>
            {
                new V1Volume(SocketVolumeName, emptyDir: new V1EmptyDirVolumeSource()),
                new V1Volume(ConfigVolumeName,configMap: v1ConfigMapVolumeSource)
            };
            var proxyMountList = new List<V1VolumeMount>
            {
                new V1VolumeMount(SocketDir,SocketVolumeName)
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
                        string name = SanitizeDNSValue(bindSubstrings[0]);
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
                        string name = SanitizeDNSValue(mount.Source);
                        string type = "DirectoryOrCreate";
                        string hostPath = mount.Source;
                        string mountPath = mount.Target;
                        bool readOnly = mount.ReadOnly;
                        volumeList.Add(new V1Volume(name, hostPath: new V1HostPathVolumeSource(hostPath, type)));
                        volumeMountList.Add(new V1VolumeMount(mountPath, name, readOnlyProperty: readOnly));
                    }
                    else if (mount.Type.Equals("volume", StringComparison.InvariantCultureIgnoreCase))
                    {
                        string name = SanitizeDNSValue(mount.Source);
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

        List<V1EnvVar> CollectEnv(IModule<AgentDocker.CombinedDockerConfig> moduleWithDockerConfig, KubernetesModuleIdentity identity)
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

        async Task WatchPodEventsAsync(WatchEventType type, V1Pod item)
        {
            // if the pod doesn't have the module label set then we are not interested in it
            if (item.Metadata.Labels.ContainsKey(Constants.k8sEdgeModuleLabel) == false)
            {
                return;
            }

            string podName = item.Metadata.Labels[Constants.k8sEdgeModuleLabel];
            Events.PodStatus(type, podName);
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
                        if (! this.moduleRuntimeInfos.TryRemove(podName, out removedRuntimeInfo))
                        {
                            Events.PodStatusRemoveError(podName);
                        }
                    }
                    break;

            }
        }

        (ModuleStatus, string) ConvertPodStatusToModuleStatus(Option<V1ContainerStatus> podStatus)
        {
            // TODO: Possibly refine this?
            return podStatus.Map(pod =>
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


        Option<V1ContainerStatus> GetContainerByName(string name, V1Pod pod)
        {
            string containerName = SanitizeDNSValue(name);
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

        (int, Option<DateTime>, Option<DateTime>, string image) GetRuntimedata(V1ContainerStatus status)
        {
            if (status?.LastState?.Running != null)
            {
                if (status.LastState.Running.StartedAt.HasValue)
                {
                    return (0, Option.Some(status.LastState.Running.StartedAt.Value), Option.None<DateTime>(), status.Image);
                }
            }
            else if (status?.LastState?.Terminated != null)
            {
                if (status.LastState.Terminated.StartedAt.HasValue &&
                    status.LastState.Terminated.FinishedAt.HasValue)
                {
                    return (0, Option.Some(status.LastState.Terminated.StartedAt.Value), Option.Some(status.LastState.Terminated.FinishedAt.Value), status.Image);
                }
            }
            return (0, Option.None<DateTime>(), Option.None<DateTime>(), String.Empty);
        }

        ModuleRuntimeInfo ConvertPodToRuntime(string name, V1Pod pod)
        {
            string moduleName = name;
            pod.Metadata?.Annotations?.TryGetValue(Constants.k8sEdgeOriginalModuleId, out moduleName);

            Option<V1ContainerStatus> containerStatus = this.GetContainerByName(name, pod);
            (ModuleStatus moduleStatus, string statusDescription) = this.ConvertPodStatusToModuleStatus(containerStatus);
            (int exitCode, Option<DateTime> startTime, Option<DateTime> exitTime, string imageHash) = this.GetRuntimedata(containerStatus.OrDefault());

            var reportedConfig = new AgentDocker.DockerReportedConfig(string.Empty, string.Empty, imageHash);
            return new ModuleRuntimeInfo<AgentDocker.DockerReportedConfig>(
                ModuleIdentityHelper.GetModuleName(moduleName), "docker", moduleStatus, statusDescription, exitCode,
                startTime, exitTime, reportedConfig);
        }

        static string SanitizeK8sValue(string name)
        {
            name = name.ToLower();

            var output = new StringBuilder();
            for (int i = 0, len = 0; i < name.Length && len < MAX_K8S_VALUE_LENGTH; i++)
            {
                if (IsAlphaNumeric(name[i]) || ALLOWED_CHARS_GENERIC.IndexOf(name[i]) != -1)
                {
                    output.Append(name[i]);
                    len++;
                }
            }

            return output.ToString();
        }

        static string SanitizeDNSValue(string name)
        {
            // The name returned from here must conform to following rules (as per RFC 1035):
            //  - length must be <= 63 characters
            //  - must be all lower case alphanumeric characters or '-'
            //  - must start with an alphabet
            //  - must end with an alphanumeric character

            name = name.ToLower();

            // get index of first character from the left that is an alphabet
            int start = 0;
            while (start < name.Length && !IsAsciiLowercase(name[start]))
            {
                start++;
            }

            // get index of last character from right that's an alphanumeric
            int end = Math.Max(start, name.Length - 1);
            while (end > start && !IsAlphaNumeric(name[end]))
            {
                end--;
            }

            // build a new string from start-end (inclusive) excluding characters
            // that aren't alphanumeric or the symbol '-'
            var output = new StringBuilder();
            for (int i = start, len = 0; i <= end && len < MAX_DNS_NAME_LENGTH; i++)
            {
                if (IsAlphaNumeric(name[i]) || ALLOWED_CHARS_DNS.IndexOf(name[i]) != -1)
                {
                    output.Append(name[i]);
                    len++;
                }
            }

            return output.ToString();
        }

        static string SanitizeLabelValue(string name)
        {
            // The name returned from here must conform to following rules:
            //  - length must be <= 63 characters
            //  - must be all lower case alphanumeric characters or ['-','.','_']
            //  - must start with an alphabet
            //  - must end with an alphanumeric character

            name = name.ToLower();

            // get index of first character from the left that is an alphabet
            int start = 0;
            while (start < name.Length && !IsAlphaNumeric(name[start]))
            {
                start++;
            }

            // get index of last character from right that's an alphanumeric
            int end = Math.Max(start, name.Length - 1);
            while (end > start && !IsAlphaNumeric(name[end]))
            {
                end--;
            }

            // build a new string from start-end (inclusive) excluding characters
            // that aren't alphanumeric or the symbol '-'
            var output = new StringBuilder();
            for (int i = start, len = 0; i <= end && len < MAX_LABEL_VALUE_LENGTH; i++)
            {
                if (IsAlphaNumeric(name[i]) || ALLOWED_CHARS_LABEL_VALUES.IndexOf(name[i]) != -1)
                {
                    output.Append(name[i]);
                    len++;
                }
            }

            return output.ToString();
        }

        static bool IsAsciiLowercase(char ch) => LOWER_ASCII.IndexOf(ch) != -1;

        static bool IsAlphaNumeric(char ch) => IsAsciiLowercase(ch) || Char.IsDigit(ch);

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeOperator>();
            const int IdStart = AgentEventIds.KubernetesOperator;

            enum EventIds
            {
                InvalidModuleType = IdStart,
                ExceptionInPodWatch,
                ExceptionInCustomResourceWatch,
                InvalidCreationString,
                ExposedPortValue,
                PortBindingValue,
                EdgeDeploymentDeserializeFail,
                DeploymentStatus,
                DeploymentError,
                DeploymentNameMismatch,
                PodStatus,
                PodStatusRemoveError,
                UpdateService,
                CreateService,
                RemoveDeployment,
                UpdateDeployment,
                CreateDeployment,
                NullListResponse,
                DeletingService,
                DeletingDeployment,
                CreatingDeployment,
                CreatingService,
                ReplacingDeployment,
                PodWatchClosed,
                CrdWatchClosed,
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

            public static void InvalidModuleType(KubernetesModule module)
            {
                Log.LogError((int)EventIds.InvalidModuleType, $"Module {module.Module.Name} has an invalid module type '{module.Module.Type}'. Expected type 'docker'");
            }

            public static void ExceptionInPodWatch(Exception ex)
            {
                Log.LogError((int)EventIds.ExceptionInPodWatch, ex, "Exception caught in Pod Watch task.");
            }
            public static void ExceptionInCustomResourceWatch(Exception ex)
            {
                Log.LogError((int)EventIds.ExceptionInCustomResourceWatch, ex, "Exception caught in Custom Resource Watch task.");
            }
            public static void InvalidCreationString(string kind, string name)
            {
                Log.LogDebug((int)EventIds.InvalidCreationString, $"Expected a valid '{kind}' creation string in k8s Object '{name}'.");
            }

            public static void ExposedPortValue(string portEntry)
            {
                Log.LogWarning((int)EventIds.ExposedPortValue, $"Received an invalid exposed port value '{portEntry}'.");
            }

            public static void PortBindingValue(KubernetesModule module, string portEntry)
            {
                Log.LogWarning((int)EventIds.PortBindingValue, $"Module {module.Module.Name} has an invalid port binding value '{portEntry}'.");
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

            public static void PodStatus(WatchEventType type, string podname)
            {
                Log.LogDebug((int)EventIds.PodStatus, $"Pod '{podname}', status'{type}'");
            }

            public static void PodStatusRemoveError(string podname)
            {
                Log.LogWarning((int)EventIds.PodStatusRemoveError, $"Notified of pod {podname} deleted, but not removed from our pod list");
            }

            public static void UpdateService(string name)
            {
                Log.LogDebug((int)EventIds.UpdateService, $"Updating service object '{name}'");
            }

            public static void CreateService(string name)
            {
                Log.LogDebug((int)EventIds.CreateService, $"Creating service object '{name}'");
            }
            public static void RemoveDeployment(string name)
            {
                Log.LogDebug((int)EventIds.RemoveDeployment, $"Removing edge deployment '{name}'");
            }
            public static void UpdateDeployment(string name)
            {
                Log.LogDebug((int)EventIds.UpdateDeployment, $"Updating edge deployment '{name}'");
            }
            public static void CreateDeployment(string name)
            {
                Log.LogDebug((int)EventIds.CreateDeployment, $"Creating edge deployment '{name}'");
            }

            public static void NullListResponse(string listType, string what)
            {
                Log.LogError((int)EventIds.NullListResponse, $"{listType} returned null {what}");
            }

            public static void PodWatchClosed()
            {
                Log.LogInformation((int)EventIds.PodWatchClosed, $"K8s closed the pod watch. Attempting to reopen watch.");
            }

            public static void CrdWatchClosed()
            {
                Log.LogInformation((int)EventIds.CrdWatchClosed, $"K8s closed the CRD watch. Attempting to reopen watch.");
            }
        }
    }
}
