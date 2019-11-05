// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using k8s;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    // TODO add unit tests
    public class EdgeDeploymentOperator : IEdgeDeploymentOperator
    {
        readonly IKubernetes client;
        readonly AsyncLock watchLock = new AsyncLock();
        readonly IEdgeDeploymentController controller;
        readonly ResourceName resourceName;
        readonly string deviceNamespace;
        readonly JsonSerializerSettings serializerSettings;
        Option<Watcher<EdgeDeploymentDefinition>> operatorWatch;
        ModuleSet currentModules;
        EdgeDeploymentStatus currentStatus;

        static readonly EdgeDeploymentStatus DefaultStatus = new EdgeDeploymentStatus(EdgeDeploymentStatusType.Failure, string.Empty);

        public EdgeDeploymentOperator(
            ResourceName resourceName,
            string deviceNamespace,
            IKubernetes client,
            IEdgeDeploymentController controller)
        {
            this.deviceNamespace = Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace));
            this.resourceName = Preconditions.CheckNotNull(resourceName, nameof(resourceName));

            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.operatorWatch = Option.None<Watcher<EdgeDeploymentDefinition>>();
            this.controller = Preconditions.CheckNotNull(controller, nameof(controller));

            this.serializerSettings = EdgeDeploymentSerialization.SerializerSettings;
            this.currentModules = ModuleSet.Empty;
            this.currentStatus = DefaultStatus;
        }

        public void Start() => this.StartListEdgeDeployments();

        public void Stop()
        {
            // TODO do we need lock here?
            this.operatorWatch.ForEach(watch => watch.Dispose());
        }

        public void Dispose() => this.Stop();

        void StartListEdgeDeployments() =>
            this.client.ListNamespacedCustomObjectWithHttpMessagesAsync(KubernetesConstants.EdgeDeployment.Group, KubernetesConstants.EdgeDeployment.Version, this.deviceNamespace, KubernetesConstants.EdgeDeployment.Plural, watch: true)
                .ContinueWith(this.OnListEdgeDeploymentsCompleted);

        async Task OnListEdgeDeploymentsCompleted(Task<HttpOperationResponse<object>> task)
        {
            HttpOperationResponse<object> response = await task;

            this.operatorWatch = Option.Some(
                response.Watch<EdgeDeploymentDefinition>(
                    onEvent: async (type, item) => await this.EdgeDeploymentOnEventHandlerAsync(type, item),
                    onClosed: () =>
                    {
                        Events.EdgeDeploymentWatchClosed();

                        // get rid of the current edge deployment watch object since we got closed
                        this.operatorWatch.ForEach(watch => watch.Dispose());
                        this.operatorWatch = Option.None<Watcher<EdgeDeploymentDefinition>>();

                        // kick off a new watch
                        this.StartListEdgeDeployments();
                    },
                    onError: Events.EdgeDeploymentWatchFailed));
        }

        internal async Task EdgeDeploymentOnEventHandlerAsync(WatchEventType type, EdgeDeploymentDefinition item)
        {
            using (await this.watchLock.LockAsync())
            {
                try
                {
                    await this.HandleEdgeDeploymentChangedAsync(type, item);
                }
                catch (Exception ex)
                {
                    Events.EdgeDeploymentWatchFailed(ex);
                    await this.ReportDeploymentFailure(ex, item);
                    throw;
                }
            }
        }

        async Task HandleEdgeDeploymentChangedAsync(WatchEventType type, EdgeDeploymentDefinition edgeDeploymentCrdObject)
        {
            // only operate on the device that matches this operator.
            if (!this.resourceName.Equals(edgeDeploymentCrdObject.Metadata.Name))
            {
                Events.DeploymentNameMismatch(edgeDeploymentCrdObject.Metadata.Name, this.resourceName);
                return;
            }

            Events.DeploymentStatus(type, this.resourceName);
            switch (type)
            {
                case WatchEventType.Added:
                case WatchEventType.Modified:
                    var desiredModules = ModuleSet.Create(edgeDeploymentCrdObject.Spec.ToArray());
                    var status = await this.controller.DeployModulesAsync(desiredModules, this.currentModules);
                    await this.ReportEdgeDeploymentStatus(edgeDeploymentCrdObject, status);
                    this.currentModules = desiredModules;
                    this.currentStatus = status;
                    break;

                case WatchEventType.Deleted:
                    // Kubernetes garbage collection will handle cleanup of deployment artifacts
                    this.currentModules = ModuleSet.Empty;
                    this.currentStatus = DefaultStatus;
                    break;

                case WatchEventType.Error:
                    Events.DeploymentError();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        async Task ReportDeploymentFailure(Exception ex, EdgeDeploymentDefinition item)
        {
            var status = EdgeDeploymentStatus.Failure(ex);
            await this.ReportEdgeDeploymentStatus(item, status);
            this.currentStatus = status;
        }

        async Task ReportEdgeDeploymentStatus(EdgeDeploymentDefinition edgeDeploymentDefinition, EdgeDeploymentStatus status)
        {
            if (!status.Equals(this.currentStatus))
            {
                var edgeDeploymentStatus = new EdgeDeploymentDefinition(
                    edgeDeploymentDefinition.ApiVersion,
                    edgeDeploymentDefinition.Kind,
                    edgeDeploymentDefinition.Metadata,
                    edgeDeploymentDefinition.Spec,
                    status);

                var crdObject = JObject.FromObject(edgeDeploymentStatus, JsonSerializer.Create(this.serializerSettings));

                try
                {
                    await this.client.ReplaceNamespacedCustomObjectStatusWithHttpMessagesAsync(
                        crdObject,
                        KubernetesConstants.EdgeDeployment.Group,
                        KubernetesConstants.EdgeDeployment.Version,
                        this.deviceNamespace,
                        KubernetesConstants.EdgeDeployment.Plural,
                        this.resourceName);
                }
                catch (HttpOperationException e)
                {
                    Events.DeploymentStatusFailed(e);
                }
            }
        }

        static class Events
        {
            const int IdStart = KubernetesEventIds.EdgeDeploymentOperator;
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeDeploymentOperator>();

            enum EventIds
            {
                WatchFailed = IdStart,
                DeploymentStatus,
                DeploymentError,
                DeploymentNameMismatch,
                DeploymentStatusFailed,
                WatchClosed,
            }

            public static void EdgeDeploymentWatchFailed(Exception ex)
            {
                Log.LogError((int)EventIds.WatchFailed, ex, "Exception caught in edge deployment watch task.");
            }

            public static void DeploymentStatus(WatchEventType type, ResourceName name)
            {
                Log.LogDebug((int)EventIds.DeploymentStatus, $"Deployment '{name}', status'{type}'");
            }

            public static void DeploymentError()
            {
                Log.LogError((int)EventIds.DeploymentError, "Operator received error on watch type.");
            }

            public static void DeploymentNameMismatch(string received, ResourceName expected)
            {
                Log.LogDebug((int)EventIds.DeploymentNameMismatch, $"Watching for edge deployments for '{expected}', received notification for '{received}'");
            }

            public static void DeploymentStatusFailed(Exception ex)
            {
                Log.LogWarning((int)EventIds.DeploymentStatusFailed, ex, "Failed to update Deployment status.");
            }

            public static void EdgeDeploymentWatchClosed()
            {
                Log.LogInformation((int)EventIds.WatchClosed, "K8s closed the edge deployment watch. Attempting to reopen watch.");
            }
        }
    }
}
