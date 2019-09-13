// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Threading.Tasks;
    using k8s;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;

    public class EdgeDeploymentOperator : IKubernetesOperator
    {
        readonly IKubernetes client;
        readonly AsyncLock watchLock = new AsyncLock();
        readonly IEdgeDeploymentController controller;
        readonly string iotHubHostname;
        readonly string deviceId;
        readonly string resourceName;
        readonly string k8sNamespace;
        Option<Watcher<EdgeDeploymentDefinition>> operatorWatch;
        ModuleSet currentModules;

        public EdgeDeploymentOperator(
            string iotHubHostname,
            string deviceId,
            string k8sNamespace,
            IKubernetes client,
            IEdgeDeploymentController controller)
        {
            this.iotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.k8sNamespace = Preconditions.CheckNonWhiteSpace(k8sNamespace, nameof(k8sNamespace));
            this.resourceName = KubeUtils.SanitizeK8sValue(this.iotHubHostname) + Constants.K8sNameDivider + KubeUtils.SanitizeK8sValue(this.deviceId);

            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.operatorWatch = Option.None<Watcher<EdgeDeploymentDefinition>>();
            this.controller = Preconditions.CheckNotNull(controller, nameof(controller));

            this.currentModules = ModuleSet.Empty;
        }

        public void Start()
        {
            // The following "List..." requests do not return until there is something to return, so if we "await" here,
            // there is a chance that one or both of these requests will block forever - we won't start creating these pods and CRDs
            // until we receive a deployment.
            // Considering setting up these watches is critical to the operation of EdgeAgent, throwing an exception and letting the process crash
            // is an acceptable fate if these tasks fail.
            this.client.ListNamespacedCustomObjectWithHttpMessagesAsync(Constants.K8sCrdGroup, Constants.K8sApiVersion, this.k8sNamespace, Constants.K8sCrdPlural, watch: true)
                .ContinueWith(this.OnListEdgeDeploymentsCompleted);
        }

        async Task OnListEdgeDeploymentsCompleted(Task<HttpOperationResponse<object>> task)
        {
            var response = await task;

            this.operatorWatch = Option.Some(
                response.Watch<EdgeDeploymentDefinition>(
                    onEvent: async (type, item) =>
                    {
                        try
                        {
                            await this.HandleEdgeDeploymentsChangedAsync(type, item);
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
                        this.Start();
                    },
                    onError: Events.ExceptionInCustomResourceWatch));
        }

        async Task HandleEdgeDeploymentsChangedAsync(WatchEventType type, EdgeDeploymentDefinition edgeDeploymentDefinition)
        {
            // only operate on the device that matches this operator.
            if (!string.Equals(edgeDeploymentDefinition.Metadata.Name, this.resourceName, StringComparison.OrdinalIgnoreCase))
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
                }
            }
        }

        public void Stop()
        {
            // TODO do we need lock here?
            this.operatorWatch.ForEach(watch => watch.Dispose());
        }

        public void Dispose() => this.Stop();

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesCrdWatcher;
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeDeploymentOperator>();

            enum EventIds
            {
                ExceptionInCustomResourceWatch = IdStart,
                DeploymentStatus,
                DeploymentError,
                DeploymentNameMismatch,
                CrdWatchClosed,
            }

            public static void ExceptionInCustomResourceWatch(Exception ex)
            {
                Log.LogError((int)EventIds.ExceptionInCustomResourceWatch, ex, "Exception caught in Custom Resource Watch task.");
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

            public static void CrdWatchClosed()
            {
                Log.LogInformation((int)EventIds.CrdWatchClosed, $"K8s closed the CRD watch. Attempting to reopen watch.");
            }
        }
    }
}
