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
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(150);

        [Unit]
        [Fact]
        public void ConstructorChecksNull()
        {
            Assert.Throws<ArgumentNullException>(() => new KubernetesRuntimeInfoProvider(null));
        }


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
        public async void GetModuleLogsTest()
        {
            var client = new Mock<IKubernetes>(MockBehavior.Strict);
            var k8sRuntimeInfo = new KubernetesRuntimeInfoProvider(client.Object);
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
            var client = new Mock<IKubernetes>(MockBehavior.Strict); //Mock.Of<IKubernetes>(kc => kc.ListNodeAsync() == Task.FromResult(k8SNodes));
            client.Setup(
                kc =>
                    kc.ListNodeWithHttpMessagesAsync(null, null, null, null, null, null, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(() => response);
            var k8sRuntimeInfo = new KubernetesRuntimeInfoProvider(client.Object);

            var result = await k8sRuntimeInfo.GetSystemInfo();
            Assert.Equal(expectedInfo.Architecture, result.Architecture);
            Assert.Equal(expectedInfo.OperatingSystemType, result.OperatingSystemType);
            Assert.Equal(expectedInfo.Version, result.Version);
            client.VerifyAll();
        }

        private static string BuildWatchEventStreamLine(string podlist, WatchEventType eventType, int podNumber = 0)
        {
            var corev1PodList = JsonConvert.DeserializeObject<V1PodList>(podlist);
            return JsonConvert.SerializeObject(new Watcher<V1Pod>.WatchEvent
            {
                Type = eventType,
                Object = corev1PodList.Items[podNumber]
            }, new StringEnumConverter());
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
                var client = new Kubernetes(new KubernetesClientConfiguration
                {
                    Host = server.Uri.ToString()
                });

                var k8sRuntimeInfo = new KubernetesRuntimeInfoProvider(client);

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
                }
                Assert.Equal(3, uniqueModules.Count);
            }
        }
    }
}
