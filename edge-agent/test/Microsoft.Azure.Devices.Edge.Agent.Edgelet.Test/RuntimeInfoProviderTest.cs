// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json.Linq;
    using Xunit;
    using EnvVar = Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode.EnvVar;

    [Unit]
    public class RuntimeInfoProviderTest
    {
        [Fact]
        public async Task GetSystemInfoTest()
        {
            // Arrange
            var moduleManager = Mock.Of<IModuleManager>();
            IRuntimeInfoProvider runtimeInfoProvider = new RuntimeInfoProvider<TestConfig>(moduleManager);

            // Act
            SystemInfo systemInfo = await runtimeInfoProvider.GetSystemInfo();

            // Assert
            Assert.NotNull(systemInfo);
            Assert.Equal(string.Empty, systemInfo.Architecture);
            Assert.Equal(string.Empty, systemInfo.OperatingSystemType);
        }

        [Fact]
        public async Task GetModulesTest()
        {
            // Arrange
            string module1Hash = Guid.NewGuid().ToString();
            var module1 = new ModuleDetails
            {
                Id = Guid.NewGuid().ToString(),
                Name = "module1",
                Status = new Status
                {
                    StartTime = new DateTime(2010, 01, 02, 03, 04, 05),
                    RuntimeStatus = new RuntimeStatus { Status = "Running", Description = "running" },
                    ExitStatus = null
                },
                Type = "docker",
                Config = new Config
                {
                    Env = new ObservableCollection<EnvVar>(new List<EnvVar> { new EnvVar { Key = "k1", Value = "v1" } }),
                    Settings = JObject.FromObject(new TestConfig(module1Hash))
                }
            };

            string module2Hash = Guid.NewGuid().ToString();
            var module2 = new ModuleDetails
            {
                Id = Guid.NewGuid().ToString(),
                Name = "module2",
                Status = new Status
                {
                    StartTime = new DateTime(2011, 02, 03, 04, 05, 06),
                    RuntimeStatus = new RuntimeStatus { Status = "Stopped", Description = "stopped" },
                    ExitStatus = new ExitStatus { ExitTime = new DateTime(2011, 02, 03, 05, 06, 07), StatusCode = "5" }
                },
                Type = "docker",
                Config = new Config
                {
                    Env = new ObservableCollection<EnvVar>(new List<EnvVar> { new EnvVar { Key = "k2", Value = "v2" } }),
                    Settings = JObject.FromObject(new TestConfig(module2Hash))
                }
            };

            var modules = new List<ModuleDetails> { module1, module2 };
            var moduleManager = Mock.Of<IModuleManager>(m => m.GetModules(It.IsAny<CancellationToken>()) == Task.FromResult(modules.AsEnumerable()));
            IRuntimeInfoProvider runtimeInfoProvider = new RuntimeInfoProvider<TestConfig>(moduleManager);

            // Act
            List<ModuleRuntimeInfo> runtimeInfos = (await runtimeInfoProvider.GetModules(CancellationToken.None)).ToList();

            // Assert
            Assert.NotNull(runtimeInfos);
            Assert.Equal(2, runtimeInfos.Count);

            ModuleRuntimeInfo runtimeInfo1 = runtimeInfos[0];
            Assert.Equal("module1", runtimeInfo1.Name);
            Assert.Equal(new DateTime(2010, 01, 02, 03, 04, 05), runtimeInfo1.StartTime.OrDefault());
            Assert.Equal(ModuleStatus.Running, runtimeInfo1.ModuleStatus);
            Assert.Equal("running", runtimeInfo1.Description);
            Assert.Equal(0, runtimeInfo1.ExitCode);
            Assert.False(runtimeInfo1.ExitTime.HasValue);
            Assert.Equal((runtimeInfo1 as ModuleRuntimeInfo<TestConfig>)?.Config.ImageHash, module1Hash);

            ModuleRuntimeInfo runtimeInfo2 = runtimeInfos[1];
            Assert.Equal("module2", runtimeInfo2.Name);
            Assert.Equal(new DateTime(2011, 02, 03, 04, 05, 06), runtimeInfo2.StartTime.OrDefault());
            Assert.Equal(ModuleStatus.Stopped, runtimeInfo2.ModuleStatus);
            Assert.Equal("stopped", runtimeInfo2.Description);
            Assert.Equal(5, runtimeInfo2.ExitCode);
            Assert.Equal(new DateTime(2011, 02, 03, 05, 06, 07), runtimeInfo2.ExitTime.OrDefault());
            Assert.Equal((runtimeInfo2 as ModuleRuntimeInfo<TestConfig>)?.Config.ImageHash, module2Hash);
        }

        class TestConfig
        {
            public TestConfig(string imageHash)
            {
                this.ImageHash = imageHash;
            }

            public string ImageHash { get; }
        }
    }
}
