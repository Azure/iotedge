// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Rest;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesRuntimeInfoProviderTest
    {
        const string Namespace = "msiot-dwr-hub-dwr-ha3";

        public static IEnumerable<object[]> SystemResponseData()
        {
            var nodeFilled = new V1Node(status: new V1NodeStatus(nodeInfo: new V1NodeSystemInfo("architecture", "bootID", "containerRuntimeVersion", "kernelVersion", "kubeProxyVersion", "kubeletVersion", "machineID", "operatingSystem", "osImage", "systemUUID")));
            var emptyNode = new V1Node();
            yield return new object[] { new V1NodeList(), new SystemInfo("Kubernetes", "Kubernetes", "Kubernetes") };
            yield return new object[] { new V1NodeList(new List<V1Node> { emptyNode }), new SystemInfo("Kubernetes", "Kubernetes", "Kubernetes") };
            yield return new object[] { new V1NodeList(new List<V1Node> { nodeFilled }), new SystemInfo("Kubernetes", "Kubernetes", "Kubernetes") };
        }

        public static V1Pod CreatePodWithPodParametersOnly(string podPhase, string podReason, string podMessage)
            => new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Name = "module-a-abc123",
                    Labels = new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeModuleLabel] = "module-a"
                    },
                    Annotations = new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeOriginalModuleId] = "Module-A"
                    }
                },
                Status = new V1PodStatus
                {
                    Phase = podPhase,
                    Message = podMessage,
                    Reason = podReason,
                }
            };

        public static V1Pod CreatePodInPhaseWithContainerStatus(string podPhase, V1ContainerState containerState)
            => new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Name = "module-a-abc123",
                    Labels = new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeModuleLabel] = "module-a"
                    },
                    Annotations = new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeOriginalModuleId] = "Module-A"
                    }
                },
                Status = new V1PodStatus
                {
                    Phase = podPhase,
                    ContainerStatuses = new List<V1ContainerStatus>()
                    {
                        new V1ContainerStatus
                        {
                            Name = "module-a",
                            State = containerState,
                        }
                    }
                }
            };

        public static IEnumerable<object[]> GetListOfPodsInRunningPhase()
        {
            return new[]
            {
                new object[]
                {
                    CreatePodInPhaseWithContainerStatus("Running", new V1ContainerState(waiting: new V1ContainerStateWaiting("Waiting", "CrashBackLoopOff"))),
                    "Module in Back-off reason: CrashBackLoopOff",
                    ModuleStatus.Backoff
                },
                new object[]
                {
                    CreatePodInPhaseWithContainerStatus("Running", new V1ContainerState(terminated: new V1ContainerStateTerminated(0, reason: "Completed"))),
                    "Module Stopped reason: Completed",
                    ModuleStatus.Stopped
                },
                new object[]
                {
                    CreatePodInPhaseWithContainerStatus("Running", new V1ContainerState(terminated: new V1ContainerStateTerminated(139, reason: "Segmentation Fault"))),
                    "Module Failed reason: Segmentation Fault",
                    ModuleStatus.Failed
                },
                new object[]
                {
                    CreatePodInPhaseWithContainerStatus("Running", new V1ContainerState(running: new V1ContainerStateRunning(startedAt: DateTime.Parse("2019-06-12T16:11:22Z")))),
                    "Started at " + DateTime.Parse("2019-06-12T16:11:22Z"),
                    ModuleStatus.Running
                }
            };
        }

        public static IEnumerable<object[]> GetListOfPodsInPendingPhase()
        {
            return new[]
            {
                new object[]
                {
                    CreatePodInPhaseWithContainerStatus("Pending", new V1ContainerState(waiting: new V1ContainerStateWaiting("Waiting", "CrashBackLoopOff"))),
                    "Module in Back-off reason: CrashBackLoopOff",
                    ModuleStatus.Backoff
                },
                new object[]
                {
                    CreatePodInPhaseWithContainerStatus("Pending", new V1ContainerState(terminated: new V1ContainerStateTerminated(0, reason: "Completed"))),
                    "Module Stopped reason: Completed",
                    ModuleStatus.Stopped
                },
                new object[]
                {
                    CreatePodInPhaseWithContainerStatus("Pending", new V1ContainerState(terminated: new V1ContainerStateTerminated(139, reason: "Segmentation Fault"))),
                    "Module Failed reason: Segmentation Fault",
                    ModuleStatus.Failed
                },
                new object[]
                {
                    CreatePodInPhaseWithContainerStatus("Pending", new V1ContainerState(running: new V1ContainerStateRunning(startedAt: DateTime.Parse("2019-06-12T16:11:22Z")))),
                    "Started at " + DateTime.Parse("2019-06-12T16:11:22Z"),
                    ModuleStatus.Backoff
                }
            };
        }

        public static IEnumerable<object[]> GetListOfPodsInAbnormalPhase()
        {
            return new[]
            {
                new object[]
                {
                    CreatePodWithPodParametersOnly("Unknown", "Unknown", "Unknown"),
                    "Module status Unknown reason: Unknown with message: Unknown",
                    ModuleStatus.Unknown
                },
                new object[]
                {
                    CreatePodWithPodParametersOnly("Failed", "Terminated", "Non-zero exit code"),
                    "Module Failed reason: Terminated with message: Non-zero exit code",
                    ModuleStatus.Failed
                },
                new object[]
                {
                    CreatePodWithPodParametersOnly("Succeeded", "Completed", "Zero exit code"),
                    "Module Stopped reason: Completed with message: Zero exit code",
                    ModuleStatus.Stopped
                }
            };
        }

        [Fact]
        public void ConstructorChecksNull()
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);

            Assert.Throws<ArgumentException>(() => new KubernetesRuntimeInfoProvider(null, client.Object, moduleManager.Object));
            Assert.Throws<ArgumentNullException>(() => new KubernetesRuntimeInfoProvider("namespace ", null, moduleManager.Object));
            Assert.Throws<ArgumentNullException>(() => new KubernetesRuntimeInfoProvider("namespace", client.Object, null));
        }

        [Fact]
        public async Task ReturnsEmptyModulesWhenNoDataAvailable()
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);

            var modules = await runtimeInfo.GetModules(CancellationToken.None);

            Assert.Empty(modules);
        }

        [Fact]
        public async Task ReturnsModulesWhenModuleInfoAdded()
        {
            V1Pod edgeagent = BuildPodList()["edgeagent"];
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            runtimeInfo.CreateOrUpdateAddPodInfo(edgeagent);

            var modules = await runtimeInfo.GetModules(CancellationToken.None);

            var info = modules.Single();
            Assert.NotNull(info);
            Assert.Equal("edgeAgent", info.Name);
        }

        [Fact]
        public async Task ReturnsRestModulesWhenSomeModulesInfoRemoved()
        {
            V1Pod edgeagent = BuildPodList()["edgeagent"];
            V1Pod edgehub = BuildPodList()["edgehub"];
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            runtimeInfo.CreateOrUpdateAddPodInfo(edgeagent);
            runtimeInfo.CreateOrUpdateAddPodInfo(edgehub);
            runtimeInfo.RemovePodInfo(edgeagent);

            var modules = await runtimeInfo.GetModules(CancellationToken.None);

            var info = modules.Single();
            Assert.NotNull(info);
            Assert.Equal("edgeHub", info.Name);
        }

        [Fact]
        public async Task ReturnsModuleRuntimeInfoWhenPodsAreUpdated()
        {
            V1Pod edgeagent1 = BuildPodList()["edgeagent"];
            edgeagent1.Metadata.Name = "edgeagent_123";
            edgeagent1.Status.ContainerStatuses
                .First(c => c.Name == "edgeagent").State.Running.StartedAt = new DateTime(2019, 10, 28);
            V1Pod edgeagent2 = BuildPodList()["edgeagent"];
            edgeagent2.Metadata.Name = "edgeAgent_456";
            edgeagent2.Status.ContainerStatuses
                .First(c => c.Name == "edgeagent").State.Running.StartedAt = new DateTime(2019, 10, 29);
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            runtimeInfo.CreateOrUpdateAddPodInfo(edgeagent1);
            runtimeInfo.CreateOrUpdateAddPodInfo(edgeagent2);
            runtimeInfo.RemovePodInfo(edgeagent1);

            var modules = await runtimeInfo.GetModules(CancellationToken.None);

            var info = modules.Single();
            Assert.NotNull(info);
            Assert.Equal(info.StartTime, Option.Some(new DateTime(2019, 10, 29)));
        }

        [Fact]
        public async Task ConvertsPodsToModules()
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            foreach (V1Pod pod in BuildPodList().Values)
            {
                runtimeInfo.CreateOrUpdateAddPodInfo(pod);
            }

            var modules = (await runtimeInfo.GetModules(CancellationToken.None)).ToList();

            Assert.Equal(3, modules.Count);
            foreach (var module in modules)
            {
                Assert.Contains("Started", module.Description);
                Assert.Equal(ModuleStatus.Running, module.ModuleStatus);
                Assert.Equal(new DateTime(2019, 6, 12), module.StartTime.GetOrElse(DateTime.MinValue).Date);
                Assert.Equal("docker", module.Type);
                if (module is ModuleRuntimeInfo<DockerReportedConfig> config)
                {
                    Assert.NotEqual("unknown:unknown", config.Config.Image);
                }
            }
        }

        [Fact]
        public async Task ReturnModuleStatusWithPodConditionsWhenThereAreNoContainers()
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            V1Pod pod = CreatePodWithPodParametersOnly("Pending", string.Empty, string.Empty);
            pod.Status.Conditions = new List<V1PodCondition>()
            {
                new V1PodCondition
                {
                    LastTransitionTime = new DateTime(2020, 02, 05, 10, 10, 10),
                    Message = "Ready",
                    Reason = "Scheduling",
                },
                new V1PodCondition
                {
                    LastTransitionTime = new DateTime(2020, 02, 05, 10, 10, 15),
                    Message = "persistentvolumeclaim module-a-pvc not found",
                    Reason = "Unschedulable",
                }
            };
            runtimeInfo.CreateOrUpdateAddPodInfo(pod);
            string expectedDescription = "Module Failed with container status Unknown More Info: persistentvolumeclaim module-a-pvc not found K8s reason: Unschedulable";

            ModuleRuntimeInfo info = (await runtimeInfo.GetModules(CancellationToken.None)).Single();

            Assert.Equal(ModuleStatus.Failed, info.ModuleStatus);
            Assert.Equal(expectedDescription, info.Description);
        }

        [Fact]
        public async Task ReturnModuleStatusWithPodConditionsIsEmpty()
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            V1Pod pod = CreatePodWithPodParametersOnly("Pending", string.Empty, string.Empty);
            pod.Status.Conditions = null;
            runtimeInfo.CreateOrUpdateAddPodInfo(pod);
            string expectedDescription = "Module Failed with Unknown pod status";

            ModuleRuntimeInfo info = (await runtimeInfo.GetModules(CancellationToken.None)).Single();

            Assert.Equal(ModuleStatus.Failed, info.ModuleStatus);
            Assert.Equal(expectedDescription, info.Description);
        }

        [Theory]
        [MemberData(nameof(GetListOfPodsInRunningPhase))]
        public async Task ReturnModuleStatusWhenPodIsRunning(V1Pod pod, string description, ModuleStatus status)
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            runtimeInfo.CreateOrUpdateAddPodInfo(pod);

            ModuleRuntimeInfo info = (await runtimeInfo.GetModules(CancellationToken.None)).Single();

            Assert.Equal(status, info.ModuleStatus);
            Assert.Equal(description, info.Description);
        }

        [Theory]
        [MemberData(nameof(GetListOfPodsInPendingPhase))]
        public async Task ReturnModuleStatusWhenPodIsPending(V1Pod pod, string description, ModuleStatus status)
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            runtimeInfo.CreateOrUpdateAddPodInfo(pod);

            ModuleRuntimeInfo info = (await runtimeInfo.GetModules(CancellationToken.None)).Single();

            Assert.Equal(status, info.ModuleStatus);
            Assert.Equal(description, info.Description);
        }

        [Theory]
        [MemberData(nameof(GetListOfPodsInAbnormalPhase))]
        public async Task ReturnModuleStatusWhenPodIsAbnormal(V1Pod pod, string description, ModuleStatus status)
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            runtimeInfo.CreateOrUpdateAddPodInfo(pod);

            ModuleRuntimeInfo info = (await runtimeInfo.GetModules(CancellationToken.None)).Single();

            Assert.Equal(status, info.ModuleStatus);
            Assert.Equal(description, info.Description);
        }

        static Dictionary<string, V1Pod> BuildPodList()
        {
            string content = File.ReadAllText("podwatch.txt");
            var list = JsonConvert.DeserializeObject<V1PodList>(content);
            return list.Items
                .Select(
                    pod =>
                    {
                        string name = default(string);
                        pod.Metadata.Labels?.TryGetValue(KubernetesConstants.K8sEdgeModuleLabel, out name);
                        return new { name, pod };
                    })
                .Where(item => !string.IsNullOrEmpty(item.name))
                .ToDictionary(item => item.name, item => item.pod);
        }
    }
}
