// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class RuntimeInfoProviderTest
    {
        [Fact]
        public async Task GetSystemInfoTest()
        {
            // Arrange
            var systemInfoSample = new SystemInfo("linux", "x86", "1");

            var moduleManager = Mock.Of<IModuleManager>(m => m.GetSystemInfoAsync() == Task.FromResult(systemInfoSample));
            IRuntimeInfoProvider runtimeInfoProvider = new RuntimeInfoProvider<TestConfig>(moduleManager);

            // Act
            SystemInfo systemInfo = await runtimeInfoProvider.GetSystemInfo();

            // Assert
            Assert.NotNull(systemInfo);
            Assert.Equal("linux", systemInfo.OperatingSystemType);
            Assert.Equal("x86", systemInfo.Architecture);
            Assert.Equal("1", systemInfo.Version);
        }

        [Fact]
        public async Task GetModulesTest()
        {
            // Arrange
            string module1Hash = Guid.NewGuid().ToString();
            var module1 = new ModuleRuntimeInfo<TestConfig>(
                "module1",
                "docker",
                ModuleStatus.Running,
                "running",
                0,
                Option.Some(new DateTime(2010, 01, 02, 03, 04, 05)),
                Option.None<DateTime>(),
                new TestConfig(module1Hash));

            string module2Hash = Guid.NewGuid().ToString();
            var module2 = new ModuleRuntimeInfo<TestConfig>(
                "module2",
                "docker",
                ModuleStatus.Stopped,
                "stopped",
                5,
                Option.Some(new DateTime(2011, 02, 03, 04, 05, 06)),
                Option.Some(new DateTime(2011, 02, 03, 05, 06, 07)),
                new TestConfig(module2Hash));

            var modules = new List<ModuleRuntimeInfo> { module1, module2 };
            var moduleManager = Mock.Of<IModuleManager>(m => m.GetModules<TestConfig>(It.IsAny<CancellationToken>()) == Task.FromResult(modules.AsEnumerable()));
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
