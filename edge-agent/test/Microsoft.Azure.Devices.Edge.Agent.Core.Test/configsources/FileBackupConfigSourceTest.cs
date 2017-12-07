// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.ConfigSources
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class FileBackupConfigSourceTest : IDisposable
    {
        const string TestType = "test";
        static readonly ConfigurationInfo ConfigurationInfo = new ConfigurationInfo();
        static readonly IEdgeAgentModule EdgeAgentModule = new TestAgentModule("edgeAgent", "test", new TestConfig("edge-agent"), ConfigurationInfo);
        static readonly TestRuntimeInfo TestRuntimeInfo = new TestRuntimeInfo("test");
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly IModule ValidModule1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ConfigurationInfo);
        static readonly IEdgeHubModule EdgeHubModule = new TestHubModule("edgeHub", "test", ModuleStatus.Running, new TestConfig("edge-hub:latest"), RestartPolicy.Always, ConfigurationInfo);
        static readonly IDictionary<string, IModule> Modules1 = new Dictionary<string, IModule> { ["mod1"] = ValidModule1 };
        static readonly DeploymentConfig ValidConfig1 = new DeploymentConfig("1.0", TestRuntimeInfo, new SystemModules(EdgeAgentModule, EdgeHubModule), Modules1);
        static readonly DeploymentConfigInfo ValidConfigInfo1 = new DeploymentConfigInfo(0, ValidConfig1);

        readonly string tempFileName;

        public FileBackupConfigSourceTest()
        {
            this.tempFileName = Path.GetTempFileName();
        }

        public void Dispose()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }
        }

        [Fact]
        [Unit]
        public void CreateSuccess()
        {
            var underlying = new Mock<IConfigSource>();

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, this.GetSerde()))
            {
                Assert.NotNull(configSource);
            }
        }

        [Fact]
        [Unit]
        public void InvalidInputsFails()
        {
            var underlying = new Mock<IConfigSource>();

            Assert.Throws<ArgumentException>(() => new FileBackupConfigSource("", underlying.Object, this.GetSerde()));
            Assert.Throws<ArgumentException>(() => new FileBackupConfigSource(null, underlying.Object, this.GetSerde()));
            Assert.Throws<ArgumentNullException>(() => new FileBackupConfigSource(this.tempFileName, null, this.GetSerde()));
            Assert.Throws<ArgumentNullException>(() => new FileBackupConfigSource(this.tempFileName, underlying.Object, null));
        }

        [Fact]
        [Unit]
        public async void FileBackupSuccessWhenFileNotExists()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            var underlying = new Mock<IConfigSource>();
            underlying.SetupSequence(t => t.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(ValidConfigInfo1)
                .ThrowsAsync(new InvalidOperationException());
            ISerde<DeploymentConfigInfo> serde = this.GetSerde();
            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde))
            {
                DeploymentConfigInfo config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);

                Assert.True(File.Exists(this.tempFileName));
                string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
                string returnedJson = serde.Serialize(config1);

                Assert.True(string.Equals(backupJson, returnedJson, StringComparison.OrdinalIgnoreCase));

                DeploymentConfigInfo config2 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config2);

                Assert.Equal(serde.Serialize(config1), serde.Serialize(config2));
            }
        }

        [Fact]
        [Unit]
        public async void FileBackupDoesNotHappenIfConfigSourceReportsException()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            // Arrange
            var underlying = new Mock<IConfigSource>();
            underlying.SetupSequence(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(ValidConfigInfo1)
                .ReturnsAsync(new DeploymentConfigInfo(10, new InvalidOperationException()));

            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            // Act
            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde))
            {
                // this call should fetch the config properly
                DeploymentConfigInfo config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);

                // this should cause the version with the exception to be returned
                DeploymentConfigInfo config2 = await configSource.GetDeploymentConfigInfoAsync();

                // Assert
                Assert.NotNull(config2);
                Assert.Equal(10, config2.Version);

                // this should still be the JSON from the first config - config1
                string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
                string returnedJson = serde.Serialize(config1);
                Assert.True(string.Equals(backupJson, returnedJson, StringComparison.OrdinalIgnoreCase));
            }
        }

        ISerde<DeploymentConfigInfo> GetSerde()
        {
            var moduleDeserializerTypes = new Dictionary<string, Type>
            {
                [TestType] = typeof(TestModule)
            };

            var edgeAgentDeserializerTypes = new Dictionary<string, Type>
            {
                [TestType] = typeof(TestAgentModule)
            };

            var edgeHubDeserializerTypes = new Dictionary<string, Type>
            {
                [TestType] = typeof(TestHubModule)
            };

            var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
            {
                [TestType] = typeof(TestRuntimeInfo)
            };

            var deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>
            {
                [typeof(IModule)] = moduleDeserializerTypes,
                [typeof(IEdgeAgentModule)] = edgeAgentDeserializerTypes,
                [typeof(IEdgeHubModule)] = edgeHubDeserializerTypes,
                [typeof(IRuntimeInfo)] = runtimeInfoDeserializerTypes,
            };

            ISerde<DeploymentConfigInfo> serde = new TypeSpecificSerDe<DeploymentConfigInfo>(deserializerTypesMap);
            return serde;
        }
    }
}
