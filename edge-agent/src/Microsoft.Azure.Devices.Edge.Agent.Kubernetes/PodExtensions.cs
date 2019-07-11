using k8s.Models;
using Microsoft.Azure.Devices.Edge.Agent.Core;
using Microsoft.Azure.Devices.Edge.Util;
using System;
using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    public static class PodExtensions
    {
        public static ModuleRuntimeInfo ConvertToRuntime(this V1Pod pod, string name)
        {
            string moduleName = name;
            pod.Metadata?.Annotations?.TryGetValue(Constants.K8sEdgeOriginalModuleId, out moduleName);

            Option<V1ContainerStatus> containerStatus = GetContainerByName(name, pod);
            (ModuleStatus moduleStatus, string statusDescription) = ConvertPodStatusToModuleStatus(containerStatus);
            (int exitCode, Option<DateTime> startTime, Option<DateTime> exitTime, string imageHash) = GetRuntimedata(containerStatus.OrDefault());

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

        private static Option<V1ContainerStatus> GetContainerByName(string name, V1Pod pod)
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

        private static (ModuleStatus, string) ConvertPodStatusToModuleStatus(Option<V1ContainerStatus> podStatus)
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

        private static (int, Option<DateTime>, Option<DateTime>, string image) GetRuntimedata(V1ContainerStatus status)
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
    }
}
