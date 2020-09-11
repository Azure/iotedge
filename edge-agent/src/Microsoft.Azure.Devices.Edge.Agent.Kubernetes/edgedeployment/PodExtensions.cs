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
            ReportedModuleStatus moduleStatus = ConvertPodStatusToModuleStatus(Option.Maybe(pod.Status), containerStatus);
            RuntimeData runtimeData = GetRuntimeData(containerStatus.OrDefault());

            string moduleName = string.Empty;
            if (!(pod.Metadata?.Annotations?.TryGetValue(KubernetesConstants.K8sEdgeOriginalModuleId, out moduleName) ?? false))
            {
                moduleName = name;
            }

            var reportedConfig = new AgentDocker.DockerReportedConfig(runtimeData.ImageName, string.Empty, string.Empty, Option.None<string>());
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

        static ReportedModuleStatus ConvertPodStatusToModuleStatus(Option<V1PodStatus> podStatus, Option<V1ContainerStatus> containerStatus)
        {
            return podStatus.Map(
                status =>
                {
                    switch (status.Phase)
                    {
                        case "Running":
                            {
                                return containerStatus.Map(c =>
                                {
                                    if (c.State.Waiting != null)
                                    {
                                        return new ReportedModuleStatus(ModuleStatus.Backoff, $"Module in Back-off reason: {c.State.Waiting.Reason}");
                                    }
                                    else if (c.State.Terminated != null)
                                    {
                                        if (c.State.Terminated.ExitCode != 0)
                                        {
                                            return new ReportedModuleStatus(ModuleStatus.Failed, $"Module Failed reason: {c.State.Terminated.Reason}");
                                        }
                                        else
                                        {
                                            return new ReportedModuleStatus(ModuleStatus.Stopped, $"Module Stopped reason: {c.State.Terminated.Reason}");
                                        }
                                    }
                                    else
                                    {
                                        return new ReportedModuleStatus(ModuleStatus.Running, $"Started at {c.State.Running.StartedAt}");
                                    }
                                }).GetOrElse(() => new ReportedModuleStatus(ModuleStatus.Failed, "Module Failed with Unknown container status"));
                            }

                        case "Pending":
                            {
                                return containerStatus.Map(c =>
                                {
                                    if (c.State.Waiting != null)
                                    {
                                        return new ReportedModuleStatus(ModuleStatus.Backoff, $"Module in Back-off reason: {c.State.Waiting.Reason}");
                                    }
                                    else if (c.State.Terminated != null)
                                    {
                                        if (c.State.Terminated.ExitCode != 0)
                                        {
                                            return new ReportedModuleStatus(ModuleStatus.Failed, $"Module Failed reason: {c.State.Terminated.Reason}");
                                        }
                                        else
                                        {
                                            return new ReportedModuleStatus(ModuleStatus.Stopped, $"Module Stopped reason: {c.State.Terminated.Reason}");
                                        }
                                    }
                                    else
                                    {
                                        return new ReportedModuleStatus(ModuleStatus.Backoff, $"Started at {c.State.Running.StartedAt}");
                                    }
                                }).GetOrElse(() =>
                                {
                                    if (status.Conditions != null)
                                    {
                                        var lastTransitionTime = status.Conditions.Where(p => p.LastTransitionTime.HasValue).Max(p => p.LastTransitionTime);
                                        var podConditions = status.Conditions.Where(p => p.LastTransitionTime == lastTransitionTime).Select(p => p).FirstOrDefault();
                                        return new ReportedModuleStatus(ModuleStatus.Failed, $"Module Failed with container status Unknown More Info: {podConditions.Message} K8s reason: {podConditions.Reason}");
                                    }
                                    else
                                    {
                                        return new ReportedModuleStatus(ModuleStatus.Failed, "Module Failed with Unknown pod status");
                                    }
                                });
                            }

                        case "Unknown":
                            return new ReportedModuleStatus(ModuleStatus.Unknown, $"Module status Unknown reason: {status.Reason} with message: {status.Message}");
                        case "Succeeded":
                            return new ReportedModuleStatus(ModuleStatus.Stopped, $"Module Stopped reason: {status.Reason} with message: {status.Message}");
                        case "Failed":
                            return new ReportedModuleStatus(ModuleStatus.Failed, $"Module Failed reason: {status.Reason} with message: {status.Message}");
                        default:
                            throw new InvalidOperationException($"Invalid pod status {status.Phase}");
                    }
                }).GetOrElse(() => new ReportedModuleStatus(ModuleStatus.Unknown, "Unable to get pod status"));
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
