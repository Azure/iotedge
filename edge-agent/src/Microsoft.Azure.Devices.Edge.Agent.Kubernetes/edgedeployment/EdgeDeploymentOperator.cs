// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System;
    using System.Threading.Tasks;
    using k8s;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    // TODO add unit tests
    public class EdgeDeploymentOperator : IEdgeDeploymentOperator
    {
        readonly IKubernetes client;
        readonly AsyncLock watchLock = new AsyncLock();
        readonly IEdgeDeploymentController controller;
        readonly ResourceName resourceName;
        readonly string deviceNamespace;
        Option<Watcher<EdgeDeploymentDefinition>> operatorWatch;
        ModuleSet currentModules;

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

            this.currentModules = ModuleSet.Empty;
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
                    onEvent: async (type, item) =>
                    {
                        try
                        {
                            await this.HandleEdgeDeploymentChangedAsync(type, item);
                        }
                        catch (Exception ex) when (!ex.IsFatal())
                        {
                            Events.EdgeDeploymentWatchFailed(ex);
                        }
                    },
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

        async Task HandleEdgeDeploymentChangedAsync(WatchEventType type, EdgeDeploymentDefinition edgeDeploymentDefinition)
        {
            // only operate on the device that matches this operator.
            if (!this.resourceName.Equals(edgeDeploymentDefinition.Metadata.Name))
            {
                Events.DeploymentNameMismatch(edgeDeploymentDefinition.Metadata.Name, this.resourceName);
                return;
            }

            using (await this.watchLock.LockAsync())
            {
                Events.DeploymentStatus(type, this.resourceName);
                switch (type)
                {
                    case WatchEventType.Added:
                    case WatchEventType.Modified:
                        var modules = await this.controller.DeployModulesAsync(edgeDeploymentDefinition.Spec, this.currentModules);
                        this.currentModules = modules;
                        break;

                    case WatchEventType.Deleted:
                        await this.controller.PurgeModulesAsync();
                        this.currentModules = ModuleSet.Empty;
                        break;

                    case WatchEventType.Error:
                        Events.DeploymentError();
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(type), type, null);
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

            public static void EdgeDeploymentWatchClosed()
            {
                Log.LogInformation((int)EventIds.WatchClosed, "K8s closed the edge deployment watch. Attempting to reopen watch.");
            }
        }
    }
}
