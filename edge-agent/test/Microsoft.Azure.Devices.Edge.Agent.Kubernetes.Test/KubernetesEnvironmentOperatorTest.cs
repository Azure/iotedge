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
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Nito.AsyncEx;
    using Xunit;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesEnvironmentOperatorTest
    {
        const string Ns = "msiot-dwr-hub-dwr-ha3";

        static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(150);

        [Fact]
        public async Task CollectsRemovedPods()
        {
            List<Watcher<V1Pod>.WatchEvent> events = BuildPodList().Values.Select(pod => new Watcher<V1Pod>.WatchEvent { Object = pod, Type = WatchEventType.Deleted }).ToList();

            using (var server = KubernetesApiServer.Watch(events))
            {
                var requestHandled = new AsyncCountdownEvent(events.Count);
                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });

                var collector = new RuntimeInfoCollector(requestHandled);
                var environment = new KubernetesEnvironmentOperator(Ns, collector, client);

                environment.Start();

                await Task.WhenAny(requestHandled.WaitAsync(), Task.Delay(TestTimeout));

                Assert.Equal(0, collector.Added);
                Assert.Equal(events.Count, collector.Removed);
            }
        }

        [Theory]
        [InlineData(WatchEventType.Added)]
        [InlineData(WatchEventType.Modified)]
        public async Task CollectsAddedOrModifiedPodsAsAdded(WatchEventType type)
        {
            Dictionary<string, V1Pod> pods = BuildPodList();
            List<Watcher<V1Pod>.WatchEvent> events = pods.Values.Select(pod => new Watcher<V1Pod>.WatchEvent { Object = pod, Type = type }).ToList();

            using (var server = KubernetesApiServer.Watch(events))
            {
                var requestHandled = new AsyncCountdownEvent(events.Count);
                var client = new Kubernetes(new KubernetesClientConfiguration { Host = server.Uri });

                var collector = new RuntimeInfoCollector(requestHandled);
                var environment = new KubernetesEnvironmentOperator(Ns, collector, client);

                environment.Start();

                await Task.WhenAny(requestHandled.WaitAsync(), Task.Delay(TestTimeout));

                Assert.Equal(pods.Count, collector.Added);
                Assert.Equal(0, collector.Removed);
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
                    }
                )
                .Where(item => !string.IsNullOrEmpty(item.name))
                .ToDictionary(item => item.name, item => item.pod);
        }

        class RuntimeInfoCollector : IRuntimeInfoSource
        {
            readonly AsyncCountdownEvent countdown;

            int added;

            public int Added => this.added;

            int removed;

            public RuntimeInfoCollector(AsyncCountdownEvent countdown)
            {
                this.countdown = countdown;
            }

            public int Removed => this.removed;

            public void CreateOrUpdateAddPodInfo(string podName, V1Pod pod)
            {
                Interlocked.Increment(ref this.added);
                this.countdown.Signal();
            }

            public bool RemovePodInfo(string podName)
            {
                Interlocked.Increment(ref this.removed);
                this.countdown.Signal();

                return true;
            }
        }
    }
}
