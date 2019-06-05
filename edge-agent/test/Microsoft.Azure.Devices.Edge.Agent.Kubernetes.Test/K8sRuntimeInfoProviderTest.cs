// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Commands;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Rest;
    using Moq;
    using Xunit;

    public class K8sRuntimeInfoProviderTest
    {
        [Unit]
        [Fact]
        public void ConstructorChecksNull()
        {
            Assert.Throws<ArgumentNullException>(() => new KubernetesRuntimeInfoProvider(null));
        }


        public static IEnumerable<object[]> SystemResponseData()
        {
            var nodeFilled = new V1Node(status:new V1NodeStatus(nodeInfo:new V1NodeSystemInfo("architecture", "bootID", "containerRuntimeVersion", "kernelVersion", "kubeProxyVersion", "kubeletVersion", "machineID", "operatingSystem", "osImage", "systemUUID")));
            var emptyNode = new V1Node();
            yield return new object[] { new V1NodeList(), new SystemInfo(string.Empty, string.Empty, string.Empty) };
            yield return new object[] { new V1NodeList(new List<V1Node>{ emptyNode}), new SystemInfo(string.Empty, string.Empty, string.Empty) };
            yield return new object[] { new V1NodeList(new List<V1Node>{ nodeFilled }), new SystemInfo("operatingSystem", "architecture", "osImage") };
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
            Assert.Equal(expectedInfo.Architecture,result.Architecture);
            Assert.Equal(expectedInfo.OperatingSystemType, result.OperatingSystemType);
            Assert.Equal(expectedInfo.Version, result.Version);
        }
    }
}
