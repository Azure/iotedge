// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Client;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    [Kubernetes]
    public class KubernetesEnvironmentOperatorTest : IClassFixture<KubernetesClusterFixture>, IAsyncLifetime
    {
        readonly KubernetesClient client;

        readonly KubernetesEnvironmentOperator environmentOperator;

        readonly KubernetesRuntimeInfoProvider runtimeInfoProvider;

        static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(1);

        public KubernetesEnvironmentOperatorTest(KubernetesClusterFixture fixture)
        {
            string deviceNamespace = $"device-{Guid.NewGuid()}";
            this.client = new KubernetesClient(deviceNamespace, fixture.Client);

            this.runtimeInfoProvider = new KubernetesRuntimeInfoProvider(deviceNamespace, fixture.Client, new DummyModuleManager());
            this.environmentOperator = new KubernetesEnvironmentOperator(deviceNamespace, this.runtimeInfoProvider, fixture.Client, 180);
        }

        public async Task InitializeAsync()
        {
            var mainCts = new CancellationTokenSource();
            await this.client.AddNamespaceAsync();
            this.environmentOperator.Start(mainCts);
        }

        public Task DisposeAsync()
        {
            this.environmentOperator?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task CollectsModuleRuntimeInfoWhenModuleDeploymentAdded()
        {
            var tokenSource = new CancellationTokenSource(DefaultTimeout);

            await this.AddEdgeModule("Module-A");
            await this.client.WaitUntilAnyPodsAsync("status.phase=Running", tokenSource.Token);

            ModuleRuntimeInfo moduleInfo = await this.GetModule("Module-A");

            moduleInfo.Should().NotBeNull();
            moduleInfo.ModuleStatus.Should().Be(ModuleStatus.Running);
            moduleInfo.Name.Should().Be("Module-A");
            moduleInfo.Description.Should().StartWith("Started at");
            moduleInfo.Type.Should().Be("docker");
            moduleInfo.ExitCode.Should().Be(0);
            moduleInfo.StartTime.Should().NotBeNone();
            moduleInfo.ExitTime.Should().BeNone();
        }

        [Fact]
        public async Task DoesNotCollectModuleRuntimeInfoForUnknownModules()
        {
            var tokenSource = new CancellationTokenSource(DefaultTimeout);

            var labels = new Dictionary<string, string> { ["a"] = "b" };
            await this.client.AddModuleServiceAccountAsync("foreign-pod", labels, null);
            await this.client.AddModuleDeploymentAsync("foreign-pod", labels, null);
            await this.client.WaitUntilAnyPodsAsync("status.phase=Running", tokenSource.Token);

            ModuleRuntimeInfo moduleInfo = await this.GetModule("foreign-pod");

            moduleInfo.Should().BeNull();
        }

        [Fact]
        public async Task DeletesModuleRuntimeInfoForDeletedModules()
        {
            var tokenSource = new CancellationTokenSource(DefaultTimeout * 3);

            await this.AddEdgeModule("Module-A");
            await this.client.WaitUntilAnyPodsAsync("status.phase=Running", tokenSource.Token);

            IEnumerable<ModuleRuntimeInfo> modules = await this.runtimeInfoProvider.GetModules(CancellationToken.None);
            modules.Should().HaveCount(1);

            await this.client.DeleteModuleDeploymentAsync("module-a");
            await this.client.WaitUntilPodsExactNumberAsync(0, tokenSource.Token);

            modules = await this.runtimeInfoProvider.GetModules(CancellationToken.None);
            modules.Should().BeEmpty();
        }

        [Fact]
        public async Task UpdatesModuleRuntimeInfoWhenModuleDeploymentUpdated()
        {
            var tokenSource = new CancellationTokenSource(DefaultTimeout * 3);

            await this.AddEdgeModule("Module-A");
            await this.client.WaitUntilAnyPodsAsync("status.phase=Running", tokenSource.Token);
            ModuleRuntimeInfo initialModuleInfo = await this.GetModule("Module-A");

            await this.client.ReplaceModuleImageAsync("module-a", "alpine:latest");
            await this.client.WaitUntilPodsExactNumberAsync(2, tokenSource.Token);
            await this.client.WaitUntilPodsExactNumberAsync(1, tokenSource.Token);
            ModuleRuntimeInfo updatedModuleInfo = await this.GetModule("Module-A");

            initialModuleInfo.Should().NotBeNull();
            initialModuleInfo.StartTime.Should().NotBeNone();
            initialModuleInfo.ModuleStatus.Should().Be(ModuleStatus.Running);

            updatedModuleInfo.Should().NotBeNull();
            updatedModuleInfo.StartTime.Should().NotBeNone();
            initialModuleInfo.ModuleStatus.Should().Be(ModuleStatus.Running);
            updatedModuleInfo.StartTime.OrDefault().Should().BeAfter(initialModuleInfo.StartTime.OrDefault());
        }

        async Task<ModuleRuntimeInfo> GetModule(string name)
        {
            IEnumerable<ModuleRuntimeInfo> modules = await this.runtimeInfoProvider.GetModules(CancellationToken.None);
            return modules.SingleOrDefault(module => module.Name == name);
        }

        async Task AddEdgeModule(string moduleName)
        {
            string name = moduleName.ToLowerInvariant();

            var labels = new Dictionary<string, string>
            {
                ["net.azure-devices.edge.deviceid"] = "edgy",
                ["net.azure-devices.edge.hub"] = "edgy-iothub.azure-devices.net",
                ["net.azure-devices.edge.module"] = name
            };
            var annotations = new Dictionary<string, string>
            {
                ["net.azure-devices.edge.original-moduleid"] = moduleName
            };

            await this.client.AddModuleServiceAccountAsync(name, labels, annotations);
            await this.client.AddModuleDeploymentAsync(name, labels, annotations);
        }
    }
}
