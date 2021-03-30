// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System;
    using System.Linq;
    using System.Threading;
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

    public class EdgeDeploymentOperator : IEdgeDeploymentOperator
    {
        readonly IKubernetes client;
        readonly AsyncLock watchLock = new AsyncLock();
        readonly IEdgeDeploymentController controller;
        readonly ResourceName resourceName;
        readonly string deviceNamespace;
        readonly int timeoutSeconds;
        readonly JsonSerializerSettings serializerSettings = EdgeDeploymentSerialization.SerializerSettings;
        Option<Watcher<EdgeDeploymentDefinition>> operatorWatch;
        ModuleSet currentModules = ModuleSet.Empty;
        EdgeDeploymentStatus currentStatus = EdgeDeploymentStatus.Default;

        public EdgeDeploymentOperator(
            ResourceName resourceName,
            string deviceNamespace,
            IKubernetes client,
            IEdgeDeploymentController controller,
            int timeoutSeconds)
        {
            this.deviceNamespace = Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace));
            this.resourceName = Preconditions.CheckNotNull(resourceName, nameof(resourceName));

            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.operatorWatch = Option.None<Watcher<EdgeDeploymentDefinition>>();
            this.controller = Preconditions.CheckNotNull(controller, nameof(controller));
            this.timeoutSeconds = timeoutSeconds;
        }

        public void Start(CancellationTokenSource shutdownCts) => this.StartListEdgeDeployments(shutdownCts);

        public void Stop()
        {
            // TODO do we need lock here?
            this.operatorWatch.ForEach(watch => watch.Dispose());
        }

        public void Dispose() => this.Stop();

        void StartListEdgeDeployments(CancellationTokenSource shutdownCts) =>
            this.client.ListNamespacedCustomObjectWithHttpMessagesAsync(KubernetesConstants.EdgeDeployment.Group, KubernetesConstants.EdgeDeployment.Version, this.deviceNamespace, KubernetesConstants.EdgeDeployment.Plural, timeoutSeconds: this.timeoutSeconds, watch: true)
                .ContinueWith(this.OnListEdgeDeploymentsCompleted, shutdownCts);

        async Task OnListEdgeDeploymentsCompleted(Task<HttpOperationResponse<object>> task, object shutdownCtsObject)
        {
            // The cts object is coming from an external source, check it and put it into an Option for safe handling.
            Option<CancellationTokenSource> shutdownCts = Option.Maybe(shutdownCtsObject as CancellationTokenSource);

            HttpOperationResponse<object> response;
            try
            {
                response = await task;
            }
            catch (Exception ex)
            {
                Events.ListEdgeDeploymentFailed(ex);
                shutdownCts.ForEach(cts => cts.Cancel());
                throw;
            }

            try
            {
                this.operatorWatch = Option.Some(
                    response.Watch<EdgeDeploymentDefinition, object>(
                        onEvent: async (type, item) => await this.EdgeDeploymentOnEventHandlerAsync(type, item, shutdownCts),
                        onClosed: () => this.RestartWatch(shutdownCts),
                        onError: (ex) => this.HandleError(ex, shutdownCts)));
            }
            catch (Exception watchEx)
            {
                Events.ContinueTaskFailed(watchEx);
                shutdownCts.ForEach(cts => cts.Cancel());
                throw;
            }
        }

        internal async Task EdgeDeploymentOnEventHandlerAsync(WatchEventType type, EdgeDeploymentDefinition item, Option<CancellationTokenSource> shutdownCts)
        {
            using (await this.watchLock.LockAsync())
            {
                try
                {
                    await this.HandleEdgeDeploymentChangedAsync(type, item);
                }
                catch (Exception ex)
                {
                    Events.EdgeDeploymentHandlerFailed(ex);
                    await this.ReportDeploymentFailure(ex, item);
                    // There are many reasons this can throw an exception, only request shutdown on fatal exceptions.
                    if (ex.IsFatal())
                    {
                        shutdownCts.ForEach(cts => cts.Cancel());
                        throw;
                    }
                }
            }
        }

        internal void RestartWatch(Option<CancellationTokenSource> shutdownCts)
        {
            Events.EdgeDeploymentWatchClosed();

            // get rid of the current edge deployment watch object since we got closed
            this.operatorWatch.ForEach(watch => watch.Dispose());
            this.operatorWatch = Option.None<Watcher<EdgeDeploymentDefinition>>();

            // kick off a new watch, log and request restart if it fails.
            try
            {
                this.StartListEdgeDeployments(shutdownCts.OrDefault());
            }
            catch (Exception ex)
            {
                Events.EdgeDeploymentWatchRestartFailed(ex);
                shutdownCts.ForEach(cts => cts.Cancel());
                throw;
            }
        }

        internal void HandleError(Exception ex, Option<CancellationTokenSource> shutdownCts)
        {
            Events.EdgeDeploymentWatchFailed(ex);
            shutdownCts.ForEach(cts => cts.Cancel());
            throw ex;
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
                    this.currentStatus = EdgeDeploymentStatus.Default;
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
                HandlerFailed,
                DeploymentStatus,
                DeploymentError,
                DeploymentNameMismatch,
                DeploymentStatusFailed,
                WatchClosed,
                WatchRestartFailed,
                ListTaskFailed,
                ContinueTaskFailed,
            }

            public static void EdgeDeploymentWatchFailed(Exception ex)
            {
                Log.LogError((int)EventIds.WatchFailed, ex, "Error event in edge deployment Watch task, requesting shutdown.");
            }

            public static void EdgeDeploymentHandlerFailed(Exception ex)
            {
                Log.LogError((int)EventIds.WatchFailed, ex, "Exception caught in edge deployment handler.");
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

            public static void EdgeDeploymentWatchRestartFailed(Exception ex)
            {
                Log.LogError((int)EventIds.WatchRestartFailed, ex, "Exception caught while attempting edge deployment watch restart, requesting shutdown.");
            }

            public static void ListEdgeDeploymentFailed(Exception ex)
            {
                Log.LogError((int)EventIds.ListTaskFailed, ex, "Exception caught while attempting to list edge deployment, requesting shutdown.");
            }

            public static void ContinueTaskFailed(Exception ex)
            {
                Log.LogError((int)EventIds.ContinueTaskFailed, ex, "Exception caught while setting up edge deployment watch, requesting shutdown.");
            }
        }
    }
}
