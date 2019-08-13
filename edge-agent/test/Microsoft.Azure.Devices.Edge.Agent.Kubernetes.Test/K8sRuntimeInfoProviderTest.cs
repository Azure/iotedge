// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Rest;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Nito.AsyncEx;
    using Xunit;

    public class K8sRuntimeInfoProviderTest
    {
        const string PodwatchNamespace = "msiot-dwr-hub-dwr-ha3";
        static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(150);

        public static IEnumerable<object[]> SystemResponseData()
        {
            var nodeFilled = new V1Node(status: new V1NodeStatus(nodeInfo: new V1NodeSystemInfo("architecture", "bootID", "containerRuntimeVersion", "kernelVersion", "kubeProxyVersion", "kubeletVersion", "machineID", "operatingSystem", "osImage", "systemUUID")));
            var emptyNode = new V1Node();
            yield return new object[] { new V1NodeList(), new SystemInfo(string.Empty, string.Empty, string.Empty) };
            yield return new object[] { new V1NodeList(new List<V1Node> { emptyNode }), new SystemInfo(string.Empty, string.Empty, string.Empty) };
            yield return new object[] { new V1NodeList(new List<V1Node> { nodeFilled }), new SystemInfo("operatingSystem", "architecture", "osImage") };
        }

        [Unit]
        [Fact]
        public void ConstructorChecksNull()
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);

            Assert.Throws<ArgumentException>(() => new KubernetesRuntimeInfoProvider(null, null));
            Assert.Throws<ArgumentNullException>(() => new KubernetesRuntimeInfoProvider("namespace ", null));
            Assert.Throws<ArgumentException>(() => new KubernetesRuntimeInfoProvider(null, client.Object));
        }

        [Unit]
        [Fact]
        public async void GetModuleLogsTest()
        {
            var response = new HttpOperationResponse<Stream>();
            response.Request = new System.Net.Http.HttpRequestMessage();
            response.Body = new MemoryStream();

            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            client.Setup(kc => kc.ReadNamespacedPodLogWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), null, true, null, null, null, null, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(() => response);
            var k8sRuntimeInfo = new KubernetesRuntimeInfoProvider(PodwatchNamespace, client.Object);
            var result = await k8sRuntimeInfo.GetModuleLogs("module", true, Option.None<int>(), Option.None<int>(), CancellationToken.None);
            Assert.True(result.Length == 0);
        }

        [Unit]
        [Theory]
        [MemberData(nameof(SystemResponseData))]
        public async void GetSystemInfoTest(V1NodeList k8SNodes, SystemInfo expectedInfo)
        {
            var response = new HttpOperationResponse<V1NodeList>();
            response.Body = k8SNodes;
            var client = new Mock<IKubernetes>(MockBehavior.Strict); // Mock.Of<IKubernetes>(kc => kc.ListNodeAsync() == Task.FromResult(k8SNodes));
            client.Setup(
                kc =>
                    kc.ListNodeWithHttpMessagesAsync(null, null, null, null, null, null, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(() => response);
            var k8sRuntimeInfo = new KubernetesRuntimeInfoProvider(PodwatchNamespace, client.Object);

            var result = await k8sRuntimeInfo.GetSystemInfo();
            Assert.Equal(expectedInfo.Architecture, result.Architecture);
            Assert.Equal(expectedInfo.OperatingSystemType, result.OperatingSystemType);
            Assert.Equal(expectedInfo.Version, result.Version);
            client.VerifyAll();
        }

        /*
         *         Added = 0,
        Modified = 1,
        Deleted = 2,
        Error = 3*/

        [Unit]
        [Fact]
        public async void PodWatchMods()
        {
            AsyncCountdownEvent requestHandled = new AsyncCountdownEvent(6);
            AsyncManualResetEvent serverShutdown = new AsyncManualResetEvent();

            var podWatchData = await File.ReadAllTextAsync("podwatch.txt");
            var addedAgent = BuildWatchEventStreamLine(podWatchData, WatchEventType.Added);
            var addedHub = BuildWatchEventStreamLine(podWatchData, WatchEventType.Added, 1);
            var addedSensor = BuildWatchEventStreamLine(podWatchData, WatchEventType.Added, 3);
            var v1PodList = JsonConvert.DeserializeObject<V1PodList>(podWatchData);
            V1Pod modAgentPod = v1PodList.Items[0];
            modAgentPod.Status.ContainerStatuses[0].State.Running = null;
            modAgentPod.Status.ContainerStatuses[0].State.Terminated = new V1ContainerStateTerminated(139, finishedAt: DateTime.Parse("2019-06-12T16:13:07Z"), startedAt: DateTime.Parse("2019-06-12T16:11:22Z"));
            var modAgent = BuildPodStreamLine(modAgentPod, WatchEventType.Modified);

            V1Pod modHubPod = v1PodList.Items[1];
            modHubPod.Status.ContainerStatuses[0].State.Running = null;
            modHubPod.Status.ContainerStatuses[1].State.Waiting = new V1ContainerStateWaiting("waiting", "reason");
            var modHub = BuildPodStreamLine(modHubPod, WatchEventType.Modified);

            V1Pod tempSensorPod = v1PodList.Items[3]; // temp sensor has a "LastState"
            tempSensorPod.Status.ContainerStatuses[1].State.Running = null;
            var modTempSensor = BuildPodStreamLine(tempSensorPod, WatchEventType.Modified);

            using (var server = new MockKubeApiServer(
                async httpContext =>
                {
                    await MockKubeApiServer.WriteStreamLine(httpContext, addedAgent);
                    await MockKubeApiServer.WriteStreamLine(httpContext, addedHub);
                    await MockKubeApiServer.WriteStreamLine(httpContext, addedSensor);
                    await MockKubeApiServer.WriteStreamLine(httpContext, modTempSensor);
                    await MockKubeApiServer.WriteStreamLine(httpContext, modHub);
                    await MockKubeApiServer.WriteStreamLine(httpContext, modAgent);
                    return false;
                }))
            {
                var client = new Kubernetes(
                    new KubernetesClientConfiguration
                    {
                        Host = server.Uri.ToString()
                    });

                var k8sRuntimeInfo = new KubernetesRuntimeInfoProvider(PodwatchNamespace, client);

                k8sRuntimeInfo.PropertyChanged += (sender, args) =>
                {
                    Assert.Equal("Modules", args.PropertyName);
                    requestHandled.Signal();
                };

                k8sRuntimeInfo.Start();

                await Task.WhenAny(requestHandled.WaitAsync(), Task.Delay(TestTimeout));
                var runtimeModules = await k8sRuntimeInfo.GetModules(CancellationToken.None);
                var moduleRuntimeInfos = runtimeModules as ModuleRuntimeInfo[] ?? runtimeModules.ToArray();

                Assert.True(moduleRuntimeInfos.Count() == 3);
                foreach (var i in moduleRuntimeInfos)
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
                        Assert.Equal(new DateTime(2019, 6, 12), i.StartTime.GetOrElse(DateTime.MinValue).Date);
                        Assert.Equal(new DateTime(2019, 6, 12), i.ExitTime.GetOrElse(DateTime.MinValue).Date);
                    }

                    if (i is ModuleRuntimeInfo<DockerReportedConfig> d)
                    {
                        Assert.NotEqual("unknown:unknown", d.Config.Image);
                    }
                }
            }
        }

        [Unit]
        [Fact]
        public async void PodWatchDelete()
        {
            AsyncCountdownEvent requestHandled = new AsyncCountdownEvent(5);
            AsyncManualResetEvent serverShutdown = new AsyncManualResetEvent();

            var podWatchData = await File.ReadAllTextAsync("podwatch.txt");
            var addedAgent = BuildWatchEventStreamLine(podWatchData, WatchEventType.Added);
            var addedHub = BuildWatchEventStreamLine(podWatchData, WatchEventType.Added, 1);
            var addedSensor = BuildWatchEventStreamLine(podWatchData, WatchEventType.Added, 3);
            var v1PodList = JsonConvert.DeserializeObject<V1PodList>(podWatchData);

            V1Pod modHubPod = v1PodList.Items[1];
            var modHub = BuildPodStreamLine(modHubPod, WatchEventType.Deleted);

            V1Pod tempSensorPod = v1PodList.Items[3];
            var modTempSensor = BuildPodStreamLine(tempSensorPod, WatchEventType.Deleted);

            using (var server = new MockKubeApiServer(
                async httpContext =>
                {
                    await MockKubeApiServer.WriteStreamLine(httpContext, addedAgent);
                    await MockKubeApiServer.WriteStreamLine(httpContext, addedHub);
                    await MockKubeApiServer.WriteStreamLine(httpContext, addedSensor);
                    await MockKubeApiServer.WriteStreamLine(httpContext, modTempSensor);
                    await MockKubeApiServer.WriteStreamLine(httpContext, modHub);
                    return false;
                }))
            {
                var client = new Kubernetes(
                    new KubernetesClientConfiguration
                    {
                        Host = server.Uri.ToString()
                    });

                var k8sRuntimeInfo = new KubernetesRuntimeInfoProvider(PodwatchNamespace, client);

                k8sRuntimeInfo.PropertyChanged += (sender, args) =>
                {
                    Assert.Equal("Modules", args.PropertyName);
                    requestHandled.Signal();
                };

                k8sRuntimeInfo.Start();

                await Task.WhenAny(requestHandled.WaitAsync(), Task.Delay(TestTimeout));
                var runtimeModules = await k8sRuntimeInfo.GetModules(CancellationToken.None);
                var moduleRuntimeInfos = runtimeModules as ModuleRuntimeInfo[] ?? runtimeModules.ToArray();

                Assert.Single(moduleRuntimeInfos);
                foreach (var i in moduleRuntimeInfos)
                {
                    Assert.Equal("edgeAgent", i.Name);
                    Assert.Equal(ModuleStatus.Running, i.ModuleStatus);
                    Assert.Equal(new DateTime(2019, 6, 12), i.StartTime.GetOrElse(DateTime.MinValue).Date);
                    Assert.Equal("docker", i.Type);
                    if (i is ModuleRuntimeInfo<DockerReportedConfig> d)
                    {
                        Assert.NotEqual("unknown:unknown", d.Config.Image);
                    }
                }
            }
        }

        [Unit]
        [Fact]
        public async void PodWatchSuccessTest()
        {
            AsyncCountdownEvent requestHandled = new AsyncCountdownEvent(3);
            AsyncManualResetEvent serverShutdown = new AsyncManualResetEvent();

            var podWatchData = await File.ReadAllTextAsync("podwatch.txt");
            var addedAgent = BuildWatchEventStreamLine(podWatchData, WatchEventType.Added);
            var addedHub = BuildWatchEventStreamLine(podWatchData, WatchEventType.Added, 1);
            var addedSensor = BuildWatchEventStreamLine(podWatchData, WatchEventType.Added, 3);
            using (var server = new MockKubeApiServer(
                async httpContext =>
                {
                    await MockKubeApiServer.WriteStreamLine(httpContext, addedAgent);
                    await MockKubeApiServer.WriteStreamLine(httpContext, addedHub);
                    await MockKubeApiServer.WriteStreamLine(httpContext, addedSensor);
                    return false;
                }))
            {
                var client = new Kubernetes(
                    new KubernetesClientConfiguration
                    {
                        Host = server.Uri.ToString()
                    });

                var k8sRuntimeInfo = new KubernetesRuntimeInfoProvider(PodwatchNamespace, client);

                k8sRuntimeInfo.PropertyChanged += (sender, args) =>
                {
                    Assert.Equal("Modules", args.PropertyName);
                    requestHandled.Signal();
                };

                k8sRuntimeInfo.Start();

                await Task.WhenAny(requestHandled.WaitAsync(), Task.Delay(TestTimeout));

                var runtimeModules = await k8sRuntimeInfo.GetModules(CancellationToken.None);
                var moduleRuntimeInfos = runtimeModules as ModuleRuntimeInfo[] ?? runtimeModules.ToArray();
                Assert.True(moduleRuntimeInfos.Count() == 3);
                Dictionary<string, int> uniqueModules = new Dictionary<string, int>();
                foreach (var i in moduleRuntimeInfos)
                {
                    uniqueModules[i.Name] = 1;
                    Assert.Contains("Started", i.Description);
                    Assert.Equal(ModuleStatus.Running, i.ModuleStatus);
                    Assert.Equal(new DateTime(2019, 6, 12), i.StartTime.GetOrElse(DateTime.MinValue).Date);
                    Assert.Equal("docker", i.Type);
                    if (i is ModuleRuntimeInfo<DockerReportedConfig> d)
                    {
                        Assert.NotEqual("unknown:unknown", d.Config.Image);
                    }
                }

                Assert.Equal(3, uniqueModules.Count);
            }
        }

        static string BuildWatchEventStreamLine(string podlist, WatchEventType eventType, int podNumber = 0)
        {
            var v1PodList = JsonConvert.DeserializeObject<V1PodList>(podlist);
            return JsonConvert.SerializeObject(
                new Watcher<V1Pod>.WatchEvent
                {
                    Type = eventType,
                    Object = v1PodList.Items[podNumber]
                },
                new StringEnumConverter());
        }

        static string BuildPodStreamLine(V1Pod pod, WatchEventType eventType)
        {
            return JsonConvert.SerializeObject(
                new Watcher<V1Pod>.WatchEvent
                {
                    Type = eventType,
                    Object = pod
                },
                new StringEnumConverter());
        }
    }
}
