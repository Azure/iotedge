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
        public async void GetModuleLogsTest()
        {
            var logs = Encoding.UTF8.GetBytes("some logs");
            var response = new HttpOperationResponse<Stream> { Request = new System.Net.Http.HttpRequestMessage(), Body = new MemoryStream(logs) };
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            client.Setup(kc => kc.ReadNamespacedPodLogWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), null, true, null, null, null, null, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(() => response);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);

            var result = await runtimeInfo.GetModuleLogs("module", true, Option.None<int>(), Option.None<int>(), CancellationToken.None);

            Assert.True(result.Length == logs.Length);
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
        public async Task ReturnsLastKnowModuleState()
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            foreach (V1Pod pod in BuildPodList().Values)
            {
                runtimeInfo.CreateOrUpdateAddPodInfo(pod);
            }

            Dictionary<string, V1Pod> modified = BuildPodList();

            string agentWaitingReason = "CrashBackLoopOff";
            modified["edgeagent"].Status.Phase = "Running";
            modified["edgeagent"].Status.ContainerStatuses[0].State.Running = null;
            modified["edgeagent"].Status.ContainerStatuses[0].State.Terminated = null;
            modified["edgeagent"].Status.ContainerStatuses[0].State.Waiting = new V1ContainerStateWaiting("Waiting", agentWaitingReason);

            string edgehubTerminatedReason = "Completed";
            modified["edgehub"].Status.Phase = "Running";
            modified["edgehub"].Status.ContainerStatuses[0].State.Running = null;
            modified["edgehub"].Status.ContainerStatuses[0].State.Waiting = null;
            modified["edgehub"].Status.ContainerStatuses[0].State.Terminated = new V1ContainerStateTerminated(0, finishedAt: DateTime.Parse("2019-06-12T16:13:07Z"), startedAt: DateTime.Parse("2019-06-12T16:11:22Z"), reason: edgehubTerminatedReason);

            modified["simulatedtemperaturesensor"].Status.Phase = "Running";
            modified["simulatedtemperaturesensor"].Status.ContainerStatuses[1].State.Running = new V1ContainerStateRunning(startedAt: DateTime.Parse("2019-06-12T16:11:22Z"));
            modified["simulatedtemperaturesensor"].Status.ContainerStatuses[1].State.Waiting = null;
            modified["simulatedtemperaturesensor"].Status.ContainerStatuses[1].State.Terminated = null;

            foreach (V1Pod pod in modified.Values)
            {
                runtimeInfo.CreateOrUpdateAddPodInfo(pod);
            }

            var runningPhaseModules = (await runtimeInfo.GetModules(CancellationToken.None)).ToList();
            Assert.Equal(3, runningPhaseModules.Count);

            foreach (var i in runningPhaseModules)
            {
                if (string.Equals("edgeAgent", i.Name))
                {
                    Assert.Equal(ModuleStatus.Backoff, i.ModuleStatus);
                    Assert.Equal($"Module in Back-off because of the reason: {agentWaitingReason}", i.Description);
                }
                else if (string.Equals("edgeHub", i.Name))
                {
                    Assert.Equal(ModuleStatus.Stopped, i.ModuleStatus);
                    Assert.Equal($"Module Stopped because of the reason: {edgehubTerminatedReason}", i.Description);
                }
                else if (string.Equals("SimulatedTemperatureSensor", i.Name))
                {
                    Assert.Equal(ModuleStatus.Running, i.ModuleStatus);
                    Assert.Equal(new DateTime(2019, 6, 12), i.StartTime.OrDefault().Date);
                }
                else
                {
                    Assert.True(false, $"Missing module {i.Name} in validation");
                }

                if (i is ModuleRuntimeInfo<DockerReportedConfig> d)
                {
                    Assert.NotEqual("unknown:unknown", d.Config.Image);
                }
            }

            agentWaitingReason = "ErrImagePull";
            modified["edgeagent"].Status.Phase = "Pending";
            modified["edgeagent"].Status.ContainerStatuses[0].State.Running = null;
            modified["edgeagent"].Status.ContainerStatuses[0].State.Terminated = null;
            modified["edgeagent"].Status.ContainerStatuses[0].State.Waiting = new V1ContainerStateWaiting("Waiting", agentWaitingReason);

            edgehubTerminatedReason = "Completed";
            modified["edgehub"].Status.Phase = "Pending";
            modified["edgehub"].Status.ContainerStatuses[0].State.Running = null;
            modified["edgehub"].Status.ContainerStatuses[0].State.Waiting = null;
            modified["edgehub"].Status.ContainerStatuses[0].State.Terminated = new V1ContainerStateTerminated(0, finishedAt: DateTime.Parse("2019-06-12T16:13:07Z"), startedAt: DateTime.Parse("2019-06-12T16:11:22Z"), reason: edgehubTerminatedReason);

            modified["simulatedtemperaturesensor"].Status.Phase = "Pending";
            modified["simulatedtemperaturesensor"].Status.ContainerStatuses[1].State.Running = new V1ContainerStateRunning(startedAt: DateTime.Parse("2019-06-12T16:11:22Z"));
            modified["simulatedtemperaturesensor"].Status.ContainerStatuses[1].State.Waiting = null;
            modified["simulatedtemperaturesensor"].Status.ContainerStatuses[1].State.Terminated = null;

            foreach (V1Pod pod in modified.Values)
            {
                runtimeInfo.CreateOrUpdateAddPodInfo(pod);
            }

            var pendingPhaseModules = (await runtimeInfo.GetModules(CancellationToken.None)).ToList();
            Assert.Equal(3, pendingPhaseModules.Count);

            foreach (var i in pendingPhaseModules)
            {
                if (string.Equals("edgeAgent", i.Name))
                {
                    Assert.Equal(ModuleStatus.Backoff, i.ModuleStatus);
                    Assert.Equal($"Module in Back-off because of the reason: {agentWaitingReason}", i.Description);
                }
                else if (string.Equals("edgeHub", i.Name))
                {
                    Assert.Equal(ModuleStatus.Stopped, i.ModuleStatus);
                    Assert.Equal($"Module Stopped becasue of the reason: {edgehubTerminatedReason}", i.Description);
                }
                else if (string.Equals("SimulatedTemperatureSensor", i.Name))
                {
                    Assert.Equal(ModuleStatus.Backoff, i.ModuleStatus);
                    Assert.Equal(new DateTime(2019, 6, 12), i.StartTime.OrDefault().Date);
                }
                else
                {
                    Assert.True(false, $"Missing module {i.Name} in validation");
                }
            }

            string agentTerminatedReason = "Segmentation Fault";
            modified["edgeagent"].Status.Phase = "Running";
            modified["edgeagent"].Status.ContainerStatuses[0].State.Running = null;
            modified["edgeagent"].Status.ContainerStatuses[0].State.Terminated = new V1ContainerStateTerminated(139, finishedAt: DateTime.Parse("2019-06-12T16:13:07Z"), startedAt: DateTime.Parse("2019-06-12T16:11:22Z"), reason: agentTerminatedReason);
            modified["edgeagent"].Status.ContainerStatuses[0].State.Waiting = null;

            edgehubTerminatedReason = "Segmentation fault";
            modified["edgehub"].Status.Phase = "Pending";
            modified["edgehub"].Status.ContainerStatuses[0].State.Running = null;
            modified["edgehub"].Status.ContainerStatuses[0].State.Waiting = null;
            modified["edgehub"].Status.ContainerStatuses[0].State.Terminated = new V1ContainerStateTerminated(139, finishedAt: DateTime.Parse("2019-06-12T16:13:07Z"), startedAt: DateTime.Parse("2019-06-12T16:11:22Z"), reason: edgehubTerminatedReason);

            modified["simulatedtemperaturesensor"].Status.Phase = "Running";
            modified["simulatedtemperaturesensor"].Status.ContainerStatuses = null;

            foreach (V1Pod pod in modified.Values)
            {
                runtimeInfo.CreateOrUpdateAddPodInfo(pod);
            }

            var failedModules = (await runtimeInfo.GetModules(CancellationToken.None)).ToList();
            Assert.Equal(3, failedModules.Count);

            foreach (var i in failedModules)
            {
                if (string.Equals("edgeAgent", i.Name))
                {
                    Assert.Equal(ModuleStatus.Failed, i.ModuleStatus);
                    Assert.Equal($"Module Failed becasue of the reason: {agentTerminatedReason}", i.Description);
                }
                else if (string.Equals("edgeHub", i.Name))
                {
                    Assert.Equal(ModuleStatus.Failed, i.ModuleStatus);
                    Assert.Equal($"Module Failed becasue of the reason: {edgehubTerminatedReason}", i.Description);
                }
                else if (string.Equals("SimulatedTemperatureSensor", i.Name))
                {
                    Assert.Equal(ModuleStatus.Failed, i.ModuleStatus);
                    Assert.Equal($"Module's container state unknown", i.Description);
                }
                else
                {
                    Assert.True(false, $"Missing module {i.Name} in validation");
                }
            }

            string agentPhaseReason = "Unable to get pod status";
            modified["edgeagent"].Status.Phase = "Unknown";
            modified["edgeagent"].Status.Reason = agentPhaseReason;

            string edgehubPhaseReason = "Module completed with zero-exit code";
            modified["edgehub"].Status.Phase = "Succeeded";
            modified["edgehub"].Status.Reason = edgehubPhaseReason;

            string sensorPhaseReason = "Module terminated with non-zero exit code";
            modified["simulatedtemperaturesensor"].Status.Phase = "Failed";
            modified["simulatedtemperaturesensor"].Status.Reason = sensorPhaseReason;

            foreach (V1Pod pod in modified.Values)
            {
                runtimeInfo.CreateOrUpdateAddPodInfo(pod);
            }

            var abnormalModules = (await runtimeInfo.GetModules(CancellationToken.None)).ToList();
            Assert.Equal(3, abnormalModules.Count);

            // Abnormal operation statuses
            foreach (var i in abnormalModules)
            {
                if (string.Equals("edgeAgent", i.Name))
                {
                    Assert.Equal(ModuleStatus.Unknown, i.ModuleStatus);
                    Assert.Equal(agentPhaseReason, i.Description);
                }
                else if (string.Equals("edgeHub", i.Name))
                {
                    Assert.Equal(ModuleStatus.Stopped, i.ModuleStatus);
                    Assert.Equal(edgehubPhaseReason, i.Description);
                }
                else if (string.Equals("SimulatedTemperatureSensor", i.Name))
                {
                    Assert.Equal(ModuleStatus.Failed, i.ModuleStatus);
                    Assert.Equal(sensorPhaseReason, i.Description);
                }
                else
                {
                    Assert.True(false, $"Missing module {i.Name} in validation");
                }
            }
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
