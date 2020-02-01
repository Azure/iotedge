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

            DateTime agentStartTime = new DateTime(2019, 6, 11);
            modified["edgeagent"].Status.Phase = "Running";
            modified["edgeagent"].Status.StartTime = agentStartTime;

            string pendingDescription = "0/1 node available";
            modified["edgehub"].Status.Phase = "Pending";
            modified["edgehub"].Status.Reason = pendingDescription;

            string finishedDescription = "Pod finished";
            modified["simulatedtemperaturesensor"].Status.Phase = "Succeeded";
            modified["simulatedtemperaturesensor"].Status.Reason = finishedDescription;
            modified["simulatedtemperaturesensor"].Status.ContainerStatuses[1].State.Running = null;
            modified["simulatedtemperaturesensor"].Status.ContainerStatuses[1].State.Terminated = new V1ContainerStateTerminated(139, finishedAt: DateTime.Parse("2019-06-12T16:13:07Z"), startedAt: DateTime.Parse("2019-06-12T16:11:22Z"));

            foreach (V1Pod pod in modified.Values)
            {
                runtimeInfo.CreateOrUpdateAddPodInfo(pod);
            }

            var modules = (await runtimeInfo.GetModules(CancellationToken.None)).ToList();
            Assert.Equal(3, modules.Count);

            // Normal operation statuses
            foreach (var i in modules)
            {
                if (string.Equals("edgeAgent", i.Name))
                {
                    Assert.Equal(ModuleStatus.Running, i.ModuleStatus);
                    Assert.Equal($"Started at {agentStartTime.ToString()}", i.Description);
                }
                else if (string.Equals("edgeHub", i.Name))
                {
                    Assert.Equal(ModuleStatus.Failed, i.ModuleStatus);
                    Assert.Equal(pendingDescription, i.Description);
                    Assert.Equal(Option.None<DateTime>(), i.ExitTime);
                }
                else if (string.Equals("SimulatedTemperatureSensor", i.Name))
                {
                    Assert.Equal(ModuleStatus.Stopped, i.ModuleStatus);
                    Assert.Equal(finishedDescription, i.Description);
                    Assert.Equal(new DateTime(2019, 6, 12), i.StartTime.OrDefault().Date);
                    Assert.Equal(new DateTime(2019, 6, 12), i.ExitTime.OrDefault().Date);
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

            string unknownDescription = "Could not reach pod";
            modified["edgeagent"].Status.Phase = "Unknown";
            modified["edgeagent"].Status.Reason = unknownDescription;

            modified["edgehub"].Status = null;

            foreach (V1Pod pod in modified.Values)
            {
                runtimeInfo.CreateOrUpdateAddPodInfo(pod);
            }

            var abnormalModules = (await runtimeInfo.GetModules(CancellationToken.None)).ToList();
            Assert.Equal(3, modules.Count);

            // Abnormal operation statuses
            foreach (var i in abnormalModules)
            {
                if (string.Equals("edgeAgent", i.Name))
                {
                    Assert.Equal(ModuleStatus.Failed, i.ModuleStatus);
                    Assert.Equal(unknownDescription, i.Description);
                }
                else if (string.Equals("edgeHub", i.Name))
                {
                    Assert.Equal(ModuleStatus.Failed, i.ModuleStatus);
                    Assert.Equal("Unable to get pod status", i.Description);
                }
                else if (string.Equals("SimulatedTemperatureSensor", i.Name))
                {
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
