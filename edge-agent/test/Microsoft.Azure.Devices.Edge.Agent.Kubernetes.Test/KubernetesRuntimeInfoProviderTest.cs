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
            Dictionary<string, V1Pod> pods = BuildPodList();
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            runtimeInfo.CreateOrUpdateAddPodInfo("edgeagent", pods["edgeagent"]);

            var modules = await runtimeInfo.GetModules(CancellationToken.None);

            var info = modules.Single();
            Assert.NotNull(info);
            Assert.Equal("edgeAgent", info.Name);
        }

        [Fact]
        public async Task ReturnsRestModulesWhenSomeModulesInfoRemoved()
        {
            Dictionary<string, V1Pod> pods = BuildPodList();
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            runtimeInfo.CreateOrUpdateAddPodInfo("edgeagent", pods["edgeagent"]);
            runtimeInfo.CreateOrUpdateAddPodInfo("edgehub", pods["edgehub"]);
            runtimeInfo.RemovePodInfo("edgeagent");

            var modules = await runtimeInfo.GetModules(CancellationToken.None);

            var info = modules.Single();
            Assert.NotNull(info);
            Assert.Equal("edgeHub", info.Name);
        }

        [Fact]
        public async Task ConvertsPodsToModules()
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var moduleManager = new Mock<IModuleManager>(MockBehavior.Strict);
            var runtimeInfo = new KubernetesRuntimeInfoProvider(Namespace, client.Object, moduleManager.Object);
            foreach ((string podName, var pod) in BuildPodList())
            {
                runtimeInfo.CreateOrUpdateAddPodInfo(podName, pod);
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
            foreach ((string podName, var pod) in BuildPodList())
            {
                runtimeInfo.CreateOrUpdateAddPodInfo(podName, pod);
            }

            Dictionary<string, V1Pod> modified = BuildPodList();
            modified["edgeagent"].Status.ContainerStatuses[0].State.Running = null;
            modified["edgeagent"].Status.ContainerStatuses[0].State.Terminated = new V1ContainerStateTerminated(139, finishedAt: DateTime.Parse("2019-06-12T16:13:07Z"), startedAt: DateTime.Parse("2019-06-12T16:11:22Z"));
            modified["edgehub"].Status.ContainerStatuses[0].State.Running = null;
            modified["edgehub"].Status.ContainerStatuses[1].State.Waiting = new V1ContainerStateWaiting("waiting", "reason");
            modified["simulatedtemperaturesensor"].Status.ContainerStatuses[1].State.Running = null;
            foreach ((string podName, var pod) in modified)
            {
                runtimeInfo.CreateOrUpdateAddPodInfo(podName, pod);
            }

            var modules = (await runtimeInfo.GetModules(CancellationToken.None)).ToList();

            Assert.Equal(3, modules.Count);
            foreach (var i in modules)
            {
                if (!string.Equals("edgeAgent", i.Name))
                {
                    Assert.Equal(ModuleStatus.Unknown, i.ModuleStatus);
                }
                else
                {
                    Assert.Equal(ModuleStatus.Failed, i.ModuleStatus);
                }

                if (string.Equals("edgeHub", i.Name))
                {
                    Assert.Equal(Option.None<DateTime>(), i.ExitTime);
                    Assert.Equal(Option.None<DateTime>(), i.StartTime);
                }
                else
                {
                    Assert.Equal(new DateTime(2019, 6, 12), i.StartTime.OrDefault().Date);
                    Assert.Equal(new DateTime(2019, 6, 12), i.ExitTime.OrDefault().Date);
                }

                if (i is ModuleRuntimeInfo<DockerReportedConfig> d)
                {
                    Assert.NotEqual("unknown:unknown", d.Config.Image);
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
