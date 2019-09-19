// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public static class PodExtensions
    {
        public static ModuleRuntimeInfo ConvertToRuntime(this V1Pod pod, string name)
        {
            Option<V1ContainerStatus> containerStatus = GetContainerByName(name, pod);
            ReportedModuleStatus moduleStatus = ConvertPodStatusToModuleStatus(containerStatus);
            RuntimeData runtimeData = GetRuntimeData(containerStatus.OrDefault());

            string moduleName = string.Empty;
            if (!(pod.Metadata?.Annotations?.TryGetValue(KubernetesConstants.K8sEdgeOriginalModuleId, out moduleName) ?? false))
            {
                moduleName = name;
            }

            var reportedConfig = new AgentDocker.DockerReportedConfig(runtimeData.ImageName, string.Empty, string.Empty);
            return new ModuleRuntimeInfo<AgentDocker.DockerReportedConfig>(
                moduleName,
                "docker",
                moduleStatus.Status,
                moduleStatus.Description,
                runtimeData.ExitStatus,
                runtimeData.StartTime,
                runtimeData.EndTime,
                reportedConfig);
        }

        static Option<V1ContainerStatus> GetContainerByName(string name, V1Pod pod)
        {
            string containerName = KubeUtils.SanitizeDNSValue(name);
            V1ContainerStatus status = pod.Status?.ContainerStatuses?
                .FirstOrDefault(container => string.Equals(container.Name, containerName, StringComparison.OrdinalIgnoreCase));
            return Option.Maybe(status);
        }

        static ReportedModuleStatus ConvertPodStatusToModuleStatus(Option<V1ContainerStatus> podStatus)
        {
            return podStatus.Map(
                pod =>
                {
                    if (pod.State != null)
                    {
                        if (pod.State.Running != null)
                        {
                            return new ReportedModuleStatus(ModuleStatus.Running, $"Started at {pod.State.Running.StartedAt.GetValueOrDefault(DateTime.Now)}");
                        }

                        if (pod.State.Terminated != null)
                        {
                            return new ReportedModuleStatus(ModuleStatus.Failed, pod.State.Terminated.Message);
                        }

                        if (pod.State.Waiting != null)
                        {
                            return new ReportedModuleStatus(ModuleStatus.Failed, pod.State.Waiting.Message);
                        }
                    }

                    return new ReportedModuleStatus(ModuleStatus.Unknown, "Unknown");
                }).GetOrElse(() => new ReportedModuleStatus(ModuleStatus.Unknown, "Unknown"));
        }

        static RuntimeData GetRuntimeData(V1ContainerStatus status)
        {
            string imageName = "unknown:unknown";
            if (status?.Image != null)
            {
                imageName = status.Image;
            }

            if (status?.State?.Running != null)
            {
                if (status.State.Running.StartedAt.HasValue)
                {
                    return new RuntimeData(0, Option.Some(status.State.Running.StartedAt.Value), Option.None<DateTime>(), imageName);
                }
            }
            else if (status?.State?.Terminated != null)
            {
                return GetTerminatedRuntimeData(status.State.Terminated, imageName);
            }
            else if (status?.LastState?.Terminated != null)
            {
                return GetTerminatedRuntimeData(status.LastState.Terminated, imageName);
            }

            return new RuntimeData(0, Option.None<DateTime>(), Option.None<DateTime>(), imageName);
        }

        static RuntimeData GetTerminatedRuntimeData(V1ContainerStateTerminated term, string imageName)
        {
            if (term.StartedAt.HasValue &&
                term.FinishedAt.HasValue)
            {
                return new RuntimeData(term.ExitCode, Option.Some(term.StartedAt.Value), Option.Some(term.FinishedAt.Value), imageName);
            }

            return new RuntimeData(0, Option.None<DateTime>(), Option.None<DateTime>(), imageName);
        }
    }

    class ReportedModuleStatus
    {
        public readonly ModuleStatus Status;
        public readonly string Description;

        public ReportedModuleStatus(ModuleStatus status, string description)
        {
            this.Status = status;
            this.Description = description;
        }
    }

    class RuntimeData
    {
        public readonly int ExitStatus;
        public readonly Option<DateTime> StartTime;
        public readonly Option<DateTime> EndTime;
        public readonly string ImageName;

        public RuntimeData(int exitStatus, Option<DateTime> startTime, Option<DateTime> endTime, string image)
        {
            this.ExitStatus = exitStatus;
            this.StartTime = startTime;
            this.EndTime = endTime;
            this.ImageName = image;
        }
    }
}
