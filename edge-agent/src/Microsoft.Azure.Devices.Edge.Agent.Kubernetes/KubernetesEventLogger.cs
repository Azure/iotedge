// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class KubernetesEventLogger<T>
    {
        private readonly ILogger Log = Logger.Factory.CreateLogger<T>();
        private const int IdStart = KubernetesEventIds.KubernetesOperator;

        public KubernetesEventLogger()
        {
        }

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
            NullNodeInfoResponse,
        }

        public void DeletingService(V1Service service)
        {
            Log.LogInformation((int)EventIds.DeletingService, $"Deleting service {service.Metadata.Name}");
        }

        public void CreatingService(V1Service service)
        {
            Log.LogInformation((int)EventIds.CreatingService, $"Creating service {service.Metadata.Name}");
        }

        public void DeletingDeployment(V1Deployment deployment)
        {
            Log.LogInformation((int)EventIds.DeletingDeployment, $"Deleting deployment {deployment.Metadata.Name}");
        }

        public void CreatingDeployment(V1Deployment deployment)
        {
            Log.LogInformation((int)EventIds.CreatingDeployment, $"Creating deployment {deployment.Metadata.Name}");
        }

        public void InvalidModuleType(IModule module)
        {
            Log.LogError((int)EventIds.InvalidModuleType, $"Module {module.Name} has an invalid module type '{module.Type}'. Expected type 'docker'");
        }

        public void ExceptionInPodWatch(Exception ex)
        {
            Log.LogError((int)EventIds.ExceptionInPodWatch, ex, "Exception caught in Pod Watch task.");
        }

        public void ExceptionInCustomResourceWatch(Exception ex)
        {
            Log.LogError((int)EventIds.ExceptionInCustomResourceWatch, ex, "Exception caught in Custom Resource Watch task.");
        }

        public void InvalidCreationString(string kind, string name)
        {
            Log.LogDebug((int)EventIds.InvalidCreationString, $"Expected a valid '{kind}' creation string in k8s Object '{name}'.");
        }

        public void ExposedPortValue(string portEntry)
        {
            Log.LogWarning((int)EventIds.ExposedPortValue, $"Received an invalid exposed port value '{portEntry}'.");
        }

        public void PortBindingValue(IModule module, string portEntry)
        {
            Log.LogWarning((int)EventIds.PortBindingValue, $"Module {module.Name} has an invalid port binding value '{portEntry}'.");
        }

        public void EdgeDeploymentDeserializeFail(Exception e)
        {
            Log.LogError((int)EventIds.EdgeDeploymentDeserializeFail, e, "Received an invalid Edge Deployment.");
        }

        public void DeploymentStatus(WatchEventType type, string name)
        {
            Log.LogDebug((int)EventIds.DeploymentStatus, $"Deployment '{name}', status'{type}'");
        }

        public void DeploymentError()
        {
            Log.LogError((int)EventIds.DeploymentError, "Operator received error on watch type.");
        }

        public void DeploymentNameMismatch(string received, string expected)
        {
            Log.LogDebug((int)EventIds.DeploymentNameMismatch, $"Watching for edge deployments for '{expected}', received notification for '{received}'");
        }

        public void PodStatus(WatchEventType type, string podname)
        {
            Log.LogDebug((int)EventIds.PodStatus, $"Pod '{podname}', status'{type}'");
        }

        public void PodStatusRemoveError(string podname)
        {
            Log.LogWarning((int)EventIds.PodStatusRemoveError, $"Notified of pod {podname} deleted, but not removed from our pod list");
        }

        public void UpdateService(string name)
        {
            Log.LogDebug((int)EventIds.UpdateService, $"Updating service object '{name}'");
        }

        public void CreateService(string name)
        {
            Log.LogDebug((int)EventIds.CreateService, $"Creating service object '{name}'");
        }

        public void RemoveDeployment(string name)
        {
            Log.LogDebug((int)EventIds.RemoveDeployment, $"Removing edge deployment '{name}'");
        }

        public void UpdateDeployment(string name)
        {
            Log.LogDebug((int)EventIds.UpdateDeployment, $"Updating edge deployment '{name}'");
        }

        public void CreateDeployment(string name)
        {
            Log.LogDebug((int)EventIds.CreateDeployment, $"Creating edge deployment '{name}'");
        }

        public void NullListResponse(string listType, string what)
        {
            Log.LogError((int)EventIds.NullListResponse, $"{listType} returned null {what}");
        }

        public void NullNodeInfoResponse(string nodeName)
        {
            Log.LogError((int) EventIds.NullNodeInfoResponse, $"node {nodeName} had no node information");
        }

        public void PodWatchClosed()
        {
            Log.LogInformation((int)EventIds.PodWatchClosed, $"K8s closed the pod watch. Attempting to reopen watch.");
        }

        public void CrdWatchClosed()
        {
            Log.LogInformation((int)EventIds.CrdWatchClosed, $"K8s closed the CRD watch. Attempting to reopen watch.");
        }
    }
}
