// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Unit]
    public class ModuleManagementHttpClientTest : IClassFixture<EdgeletFixture>
    {
        readonly Uri serverUrl;
        readonly EdgeletFixture edgeletFixture;

        public ModuleManagementHttpClientTest(EdgeletFixture edgeletFixture)
        {
            this.serverUrl = new Uri(edgeletFixture.ServiceUrl);
            this.edgeletFixture = edgeletFixture;
        }

        [Fact]
        public void VersioningTest()
        {
            string serverApiVersion = "2018-06-28";
            string clientApiVersion = "2018-06-28";
            var versionedClient = ModuleManagementHttpClient.GetVersionedModuleManagement(this.serverUrl, serverApiVersion, clientApiVersion);
            Assert.True(versionedClient is Version_2018_06_28.ModuleManagementHttpClient);

            serverApiVersion = "2018-06-28";
            clientApiVersion = "2019-01-30";
            versionedClient = ModuleManagementHttpClient.GetVersionedModuleManagement(this.serverUrl, serverApiVersion, clientApiVersion);
            Assert.True(versionedClient is Version_2018_06_28.ModuleManagementHttpClient);

            serverApiVersion = "2019-01-30";
            clientApiVersion = "2018-06-28";
            versionedClient = ModuleManagementHttpClient.GetVersionedModuleManagement(this.serverUrl, serverApiVersion, clientApiVersion);
            Assert.True(versionedClient is Version_2018_06_28.ModuleManagementHttpClient);

            serverApiVersion = "2019-01-30";
            clientApiVersion = "2019-01-30";
            versionedClient = ModuleManagementHttpClient.GetVersionedModuleManagement(this.serverUrl, serverApiVersion, clientApiVersion);
            Assert.True(versionedClient is Version_2019_01_30.ModuleManagementHttpClient);

            serverApiVersion = "2019-02-30";
            clientApiVersion = "2019-01-30";
            versionedClient = ModuleManagementHttpClient.GetVersionedModuleManagement(this.serverUrl, serverApiVersion, clientApiVersion);
            Assert.True(versionedClient is Version_2019_01_30.ModuleManagementHttpClient);

            serverApiVersion = "2019-01-30";
            clientApiVersion = "2019-02-30";
            Assert.Throws<InvalidOperationException>(() => new ModuleManagementHttpClient(this.serverUrl, serverApiVersion, clientApiVersion));
        }

        [Theory]
        [InlineData("2018-06-28", "2018-06-28")]
        [InlineData("2018-06-28", "2019-01-30")]
        [InlineData("2019-01-30", "2018-06-28")]
        [InlineData("2019-01-30", "2019-01-30")]
        public async Task IdentityTest(string serverApiVersion, string clientApiVersion)
        {
            // Arrange
            IIdentityManager client = new ModuleManagementHttpClient(this.serverUrl, serverApiVersion, clientApiVersion);

            // Act
            Identity identity1 = await client.CreateIdentityAsync("Foo", Constants.ModuleIdentityEdgeManagedByValue);
            Identity identity2 = await client.CreateIdentityAsync("Bar", Constants.ModuleIdentityEdgeManagedByValue);
            Identity identity3 = await client.CreateIdentityAsync("External", "Someone");
            Identity identity4 = await client.CreateIdentityAsync("ExternalNoManagedBy", string.Empty);

            // Assert
            Assert.NotNull(identity1);
            Assert.Equal("Foo", identity1.ModuleId);
            Assert.Equal(Constants.ModuleIdentityEdgeManagedByValue, identity1.ManagedBy);
            Assert.NotNull(identity2);
            Assert.Equal("Bar", identity2.ModuleId);
            Assert.Equal(Constants.ModuleIdentityEdgeManagedByValue, identity2.ManagedBy);
            Assert.NotNull(identity3);
            Assert.Equal("External", identity3.ModuleId);
            Assert.Equal("Someone", identity3.ManagedBy);
            Assert.Equal(string.Empty, identity4.ManagedBy);

            // Act
            Identity identity5 = await client.UpdateIdentityAsync("Foo", identity1.GenerationId, identity1.ManagedBy);
            Identity identity6 = await client.UpdateIdentityAsync("Bar", identity2.GenerationId, identity2.ManagedBy);
            Identity identity7 = await client.UpdateIdentityAsync("External", identity3.GenerationId, identity3.ManagedBy);

            // Assert
            Assert.NotNull(identity5);
            Assert.Equal("Foo", identity5.ModuleId);
            Assert.Equal(identity1.GenerationId, identity5.GenerationId);
            Assert.Equal(identity1.ManagedBy, identity5.ManagedBy);
            Assert.NotNull(identity6);
            Assert.Equal("Bar", identity6.ModuleId);
            Assert.Equal(identity2.GenerationId, identity6.GenerationId);
            Assert.Equal(identity2.ManagedBy, identity6.ManagedBy);
            Assert.NotNull(identity7);
            Assert.Equal("External", identity7.ModuleId);
            Assert.Equal(identity3.GenerationId, identity7.GenerationId);
            Assert.Equal("Someone", identity7.ManagedBy);

            // Act
            List<Identity> identities = (await client.GetIdentities())
                .OrderBy(o => o.ModuleId)
                .ToList();

            // Assert
            Assert.NotNull(identities);
            Assert.Equal(4, identities.Count);
            Assert.Equal("Bar", identities[0].ModuleId);
            Assert.Equal("External", identities[1].ModuleId);
            Assert.Equal("ExternalNoManagedBy", identities[2].ModuleId);
            Assert.Equal("Foo", identities[3].ModuleId);
            Assert.Equal(Constants.ModuleIdentityEdgeManagedByValue, identities[0].ManagedBy);
            Assert.Equal("Someone", identities[1].ManagedBy);
            Assert.Equal(string.Empty, identities[2].ManagedBy);
            Assert.Equal(Constants.ModuleIdentityEdgeManagedByValue, identities[3].ManagedBy);

            // Act
            await client.DeleteIdentityAsync("Bar");
            identities = (await client.GetIdentities())
                .OrderBy(s => s.ModuleId)
                .ToList();

            // Assert
            Assert.NotNull(identities);
            Assert.Equal(3, identities.Count);
            Assert.Equal("External", identities[0].ModuleId);
            Assert.Equal("ExternalNoManagedBy", identities[1].ModuleId);
            Assert.Equal("Foo", identities[2].ModuleId);
        }

        [Theory]
        [InlineData("2018-06-28", "2018-06-28")]
        [InlineData("2018-06-28", "2019-01-30")]
        [InlineData("2019-01-30", "2018-06-28")]
        [InlineData("2019-01-30", "2019-01-30")]
        public async Task ModulesTest(string serverApiVersion, string clientApiVersion)
        {
            // Arrange
            IModuleManager client = new ModuleManagementHttpClient(this.serverUrl, serverApiVersion, clientApiVersion);
            var moduleSpec = new ModuleSpec("Module1", "Docker", ImagePullPolicy.OnCreate, JObject.Parse("{ \"image\": \"testimage\" }"), new ObservableCollection<EnvVar> { new EnvVar("E1", "P1") });

            // Act
            await client.CreateModuleAsync(moduleSpec);
            ModuleRuntimeInfo moduleDetails = (await client.GetModules<TestConfig>(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.NotNull(moduleDetails);
            Assert.Equal("Module1", moduleDetails.Name);
            Assert.Equal("Docker", moduleDetails.Type);
            Assert.Equal(ModuleStatus.Unknown, moduleDetails.ModuleStatus);

            // Act
            await client.StartModuleAsync(moduleSpec.Name);
            moduleDetails = (await client.GetModules<TestConfig>(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.NotNull(moduleDetails);
            Assert.Equal(ModuleStatus.Running, moduleDetails.ModuleStatus);

            // Act
            await client.StopModuleAsync(moduleSpec.Name);
            moduleDetails = (await client.GetModules<TestConfig>(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.NotNull(moduleDetails);
            Assert.Equal(ModuleStatus.Stopped, moduleDetails.ModuleStatus);

            // Act
            await client.RestartModuleAsync(moduleSpec.Name);
            moduleDetails = (await client.GetModules<TestConfig>(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.NotNull(moduleDetails);
            Assert.Equal(ModuleStatus.Running, moduleDetails.ModuleStatus);

            // Act
            moduleSpec.EnvironmentVariables.ToList().Add(new EnvVar("test", "added"));
            await client.UpdateModuleAsync(moduleSpec);
            moduleDetails = (await client.GetModules<TestConfig>(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.NotNull(moduleDetails);
            Assert.Equal(ModuleStatus.Unknown, moduleDetails.ModuleStatus);

            // Act
            moduleSpec.EnvironmentVariables.ToList().Add(new EnvVar("test", "added"));
            await client.UpdateAndStartModuleAsync(moduleSpec);
            moduleDetails = (await client.GetModules<TestConfig>(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.NotNull(moduleDetails);
            Assert.Equal(ModuleStatus.Running, moduleDetails.ModuleStatus);

            // Act - Stopping a stopped module should not throw
            await client.StopModuleAsync(moduleSpec.Name);
            moduleDetails = (await client.GetModules<TestConfig>(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.NotNull(moduleDetails);
            Assert.Equal(ModuleStatus.Stopped, moduleDetails.ModuleStatus);

            // Act
            await client.DeleteModuleAsync(moduleSpec.Name);
            moduleDetails = (await client.GetModules<TestConfig>(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.Null(moduleDetails);
        }

        [Theory]
        [InlineData("2018-06-28", "2018-06-28")]
        [InlineData("2018-06-28", "2019-01-30")]
        [InlineData("2019-01-30", "2018-06-28")]
        [InlineData("2019-01-30", "2019-01-30")]
        public async Task Test_PrepareUpdate_ShouldSucceed(string serverApiVersion, string clientApiVersion)
        {
            // Arrange
            var moduleSpec = new ModuleSpec(
                "Module1",
                "Docker",
                ImagePullPolicy.OnCreate,
                JObject.Parse("{ \"image\": \"testimage\" }"),
                new ObservableCollection<EnvVar> { new EnvVar("E1", "P1") });
            IModuleManager client = new ModuleManagementHttpClient(this.serverUrl, serverApiVersion, clientApiVersion);
            await client.PrepareUpdateAsync(moduleSpec);
        }

        [Theory]
        [InlineData("2018-06-28", "2018-06-28")]
        [InlineData("2018-06-28", "2019-01-30")]
        [InlineData("2019-01-30", "2018-06-28")]
        [InlineData("2019-01-30", "2019-01-30")]
        public async Task ModuleLogsTest(string serverApiVersion, string clientApiVersion)
        {
            // Arrange
            IModuleManager client = new ModuleManagementHttpClient(this.serverUrl, serverApiVersion, clientApiVersion);

            // Act
            Stream logsStream = await client.GetModuleLogs("edgeHub", false, Option.None<int>(), Option.None<int>(), CancellationToken.None);

            // Assert
            Assert.NotNull(logsStream);
            byte[] buffer = new byte[1024];
            int bytesRead = await logsStream.ReadAsync(buffer, 0, buffer.Length);
            Assert.Equal(buffer.Length, bytesRead);
        }

        [Fact]
        public void ImagePullPolicyTest()
        {
            Assert.Equal(
                Version_2019_01_30.GeneratedCode.ImagePullPolicy.OnCreate,
                Version_2019_01_30.ModuleManagementHttpClient.ToGeneratedCodePullPolicy(ImagePullPolicy.OnCreate));
            Assert.Equal(
                Version_2019_01_30.GeneratedCode.ImagePullPolicy.Never,
                Version_2019_01_30.ModuleManagementHttpClient.ToGeneratedCodePullPolicy(ImagePullPolicy.Never));

            Assert.Throws<InvalidOperationException>(() => Version_2019_01_30.ModuleManagementHttpClient.ToGeneratedCodePullPolicy((ImagePullPolicy)int.MaxValue));
        }

        [Fact]
        public async Task ExecuteTimeoutTest_Version_2018_06_28()
        {
            // Arrange
            var client = new Version_2018_06_28.ModuleManagementHttpClient(this.serverUrl, Option.Some(TimeSpan.FromSeconds(10)));

            async Task<int> LongOperation()
            {
                await Task.Delay(TimeSpan.FromHours(1));
                return 10;
            }

            // Act
            Task assertTask = Assert.ThrowsAsync<TimeoutException>(() => client.Execute<int>(LongOperation, "Dummy"));
            Task delayTask = Task.Delay(TimeSpan.FromSeconds(20));

            Task completedTask = await Task.WhenAny(assertTask, delayTask);
            Assert.Equal(assertTask, completedTask);
        }

        [Fact]
        public async Task ExecuteTimeoutTest_Version_2019_01_30()
        {
            // Arrange
            var client = new Version_2019_01_30.ModuleManagementHttpClient(this.serverUrl, Option.Some(TimeSpan.FromSeconds(10)));

            async Task<int> LongOperation()
            {
                await Task.Delay(TimeSpan.FromHours(1));
                return 10;
            }

            // Act
            Task assertTask = Assert.ThrowsAsync<TimeoutException>(() => client.Execute<int>(LongOperation, "Dummy"));
            Task delayTask = Task.Delay(TimeSpan.FromSeconds(20));

            Task completedTask = await Task.WhenAny(assertTask, delayTask);
            Assert.Equal(assertTask, completedTask);
        }
    }
}
