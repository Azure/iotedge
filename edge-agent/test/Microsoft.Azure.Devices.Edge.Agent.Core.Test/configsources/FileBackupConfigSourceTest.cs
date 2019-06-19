// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.ConfigSources
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class FileBackupConfigSourceTest : IDisposable
    {
        const string TestType = "test";
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly ConfigurationInfo ConfigurationInfo = new ConfigurationInfo();
        static readonly IEdgeAgentModule EdgeAgentModule = new TestAgentModule("edgeAgent", "test", new TestConfig("edge-agent"), ImagePullPolicy.OnCreate, ConfigurationInfo, EnvVars);
        static readonly TestRuntimeInfo TestRuntimeInfo = new TestRuntimeInfo("test");
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly IModule ValidModule1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, ConfigurationInfo, EnvVars);
        static readonly IEdgeHubModule EdgeHubModule = new TestHubModule("edgeHub", "test", ModuleStatus.Running, new TestConfig("edge-hub:latest"), RestartPolicy.Always, ImagePullPolicy.OnCreate, ConfigurationInfo, EnvVars);
        static readonly IDictionary<string, IModule> Modules1 = new Dictionary<string, IModule> { ["mod1"] = ValidModule1 };
        static readonly IDictionary<string, IModule> Modules2 = new Dictionary<string, IModule> { ["mod2"] = ValidModule1 };
        static readonly DeploymentConfig ValidConfig1 = new DeploymentConfig("1.0", TestRuntimeInfo, new SystemModules(EdgeAgentModule, EdgeHubModule), Modules1);
        static readonly DeploymentConfig ValidConfig2 = new DeploymentConfig("1.0", TestRuntimeInfo, new SystemModules(EdgeAgentModule, EdgeHubModule), Modules2);
        static readonly DeploymentConfigInfo ValidConfigInfo1 = new DeploymentConfigInfo(0, ValidConfig1);
        static readonly DeploymentConfigInfo ValidConfigInfo2 = new DeploymentConfigInfo(0, ValidConfig2);

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

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, this.GetSerde(), NullEncryptionProvider.Instance))
            {
                Assert.NotNull(configSource);
            }
        }

        [Fact]
        [Unit]
        public void InvalidInputsFails()
        {
            var underlying = new Mock<IConfigSource>();

            Assert.Throws<ArgumentException>(() => new FileBackupConfigSource(string.Empty, underlying.Object, this.GetSerde(), NullEncryptionProvider.Instance));
            Assert.Throws<ArgumentException>(() => new FileBackupConfigSource(null, underlying.Object, this.GetSerde(), NullEncryptionProvider.Instance));
            Assert.Throws<ArgumentNullException>(() => new FileBackupConfigSource(this.tempFileName, null, this.GetSerde(), NullEncryptionProvider.Instance));
            Assert.Throws<ArgumentNullException>(() => new FileBackupConfigSource(this.tempFileName, underlying.Object, null, NullEncryptionProvider.Instance));
            Assert.Throws<ArgumentNullException>(() => new FileBackupConfigSource(this.tempFileName, underlying.Object, this.GetSerde(), null));
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
            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, NullEncryptionProvider.Instance))
            {
                DeploymentConfigInfo config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);

                Assert.True(File.Exists(this.tempFileName));
                string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
                string returnedJson = serde.Serialize(config1);

                Assert.Equal(backupJson, returnedJson, ignoreCase: true);

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
            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, NullEncryptionProvider.Instance))
            {
                // this call should fetch the config properly
                DeploymentConfigInfo config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);
                Assert.Equal(0, config1.Version);
                Assert.False(config1.Exception.HasValue);

                // this should cause the version with the exception to be returned
                DeploymentConfigInfo config2 = await configSource.GetDeploymentConfigInfoAsync();

                // Assert
                Assert.NotNull(config2);
                Assert.True(config2.Exception.HasValue);
                Assert.IsType<InvalidOperationException>(config2.Exception.OrDefault());

                // this should still be the JSON from the first config - config1
                string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
                string returnedJson = serde.Serialize(config1);
                Assert.Equal(backupJson, returnedJson, ignoreCase: true);
            }
        }

        [Fact]
        [Unit]
        public async void FileBackupDoesNotThrowWhenBackupFileDoesNotExist()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            // Arrange
            var underlying = new Mock<IConfigSource>();
            underlying.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(DeploymentConfigInfo.Empty);

            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            // Act
            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, NullEncryptionProvider.Instance))
            {
                // this call should fetch the config properly
                DeploymentConfigInfo config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);
                Assert.Equal(DeploymentConfigInfo.Empty, config1);
            }
        }

        [Fact]
        [Unit]
        public async void FileBackupDoesNotHappenIfConfigSourceEmpty()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            // Arrange
            var underlying = new Mock<IConfigSource>();
            underlying.SetupSequence(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(ValidConfigInfo1)
                .ReturnsAsync(DeploymentConfigInfo.Empty);

            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            // Act
            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, NullEncryptionProvider.Instance))
            {
                // this call should fetch the config properly
                DeploymentConfigInfo config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);
                Assert.Equal(0, config1.Version);

                // this should cause the version with the exception to be returned
                DeploymentConfigInfo config2 = await configSource.GetDeploymentConfigInfoAsync();

                // Assert
                Assert.NotNull(config2);
                Assert.Equal(0, config2.Version);
                Assert.Equal(config2.DeploymentConfig.Modules, config1.DeploymentConfig.Modules);

                // this should still be the JSON from the first config - config1
                string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
                string returnedJson = serde.Serialize(config1);
                Assert.Equal(backupJson, returnedJson, ignoreCase: true);
            }
        }

        [Fact]
        [Unit]
        public async void FileBackupSuccessCallsEncrypt()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            var underlying = new Mock<IConfigSource>();
            underlying.SetupSequence(t => t.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(ValidConfigInfo1);

            ISerde<DeploymentConfigInfo> serde = this.GetSerde();
            var encryptionProvider = new Mock<IEncryptionProvider>();
            encryptionProvider.Setup(ep => ep.EncryptAsync(It.IsAny<string>()))
                .ReturnsAsync(serde.Serialize(ValidConfigInfo1));
            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, encryptionProvider.Object))
            {
                DeploymentConfigInfo config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);

                Assert.True(File.Exists(this.tempFileName));
                string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
                string returnedJson = serde.Serialize(config1);

                Assert.Equal(backupJson, returnedJson, true);
                encryptionProvider.Verify(ep => ep.EncryptAsync(It.IsAny<string>()));
            }
        }

        [Fact]
        [Unit]
        public async void FileBackupDoesNotThrowWhenEncryptFails()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            var underlying = new Mock<IConfigSource>();
            underlying.SetupSequence(t => t.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(ValidConfigInfo1);

            ISerde<DeploymentConfigInfo> serde = this.GetSerde();
            var encryptionProvider = new Mock<IEncryptionProvider>();
            encryptionProvider.Setup(ep => ep.EncryptAsync(It.IsAny<string>()))
                .ThrowsAsync(new WorkloadCommunicationException("failed", 404));
            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, encryptionProvider.Object))
            {
                DeploymentConfigInfo config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);

                Assert.Equal(ValidConfigInfo1, config1);

                encryptionProvider.Verify(ep => ep.EncryptAsync(It.IsAny<string>()));
            }
        }

        [Fact]
        [Unit]
        public async void FileBackupReadFromBackupCallsEncryptDecrypt()
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
            var encryptionProvider = new Mock<IEncryptionProvider>();
            encryptionProvider.Setup(ep => ep.EncryptAsync(It.IsAny<string>()))
                .ReturnsAsync(serde.Serialize(ValidConfigInfo1));
            encryptionProvider.Setup(ep => ep.DecryptAsync(It.IsAny<string>()))
                .ReturnsAsync(serde.Serialize(ValidConfigInfo1));

            DeploymentConfigInfo config1;
            DeploymentConfigInfo config2;

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, encryptionProvider.Object))
            {
                config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);

                Assert.True(File.Exists(this.tempFileName));
                string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
                string returnedJson = serde.Serialize(config1);

                Assert.Equal(backupJson, returnedJson, ignoreCase: true);
            }

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, encryptionProvider.Object))
            {
                config2 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config2);
            }

            Assert.Equal(serde.Serialize(config1), serde.Serialize(config2));
            encryptionProvider.Verify(ep => ep.EncryptAsync(It.IsAny<string>()));
            encryptionProvider.Verify(ep => ep.DecryptAsync(It.IsAny<string>()));
        }

        [Fact]
        [Unit]
        public async void FileBackupShouldNotThrowWhenDecryptFails()
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
            var encryptionProvider = new Mock<IEncryptionProvider>();
            encryptionProvider.Setup(ep => ep.EncryptAsync(It.IsAny<string>()))
                .ReturnsAsync(serde.Serialize(ValidConfigInfo1));
            encryptionProvider.Setup(ep => ep.DecryptAsync(It.IsAny<string>()))
                .ThrowsAsync(new WorkloadCommunicationException("failed", 404));

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, encryptionProvider.Object))
            {
                DeploymentConfigInfo config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);

                Assert.True(File.Exists(this.tempFileName));
                string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
                string returnedJson = serde.Serialize(config1);

                Assert.Equal(backupJson, returnedJson, ignoreCase: true);
            }

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, encryptionProvider.Object))
            {
                DeploymentConfigInfo config2 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config2);

                Assert.Equal(DeploymentConfigInfo.Empty, config2);
                encryptionProvider.Verify(ep => ep.EncryptAsync(It.IsAny<string>()));
                encryptionProvider.Verify(ep => ep.DecryptAsync(It.IsAny<string>()));
            }
        }

        [Fact]
        [Unit]
        public async void FileBackupWriteOnlyWhenConfigurationChanges()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            var underlying = new Mock<IConfigSource>();
            underlying.SetupSequence(t => t.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(ValidConfigInfo1)
                .ReturnsAsync(ValidConfigInfo1)
                .ReturnsAsync(ValidConfigInfo2)
                .ReturnsAsync(ValidConfigInfo2);

            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, NullEncryptionProvider.Instance))
            {
                DeploymentConfigInfo config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);

                Assert.True(File.Exists(this.tempFileName));
                string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
                string returnedJson = serde.Serialize(config1);

                Assert.Equal(backupJson, returnedJson, ignoreCase: true);

                DateTime modifiedTime1 = File.GetLastWriteTimeUtc(this.tempFileName);
                Assert.True(DateTime.UtcNow - modifiedTime1 < TimeSpan.FromSeconds(5));

                DeploymentConfigInfo config2 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config2);

                Assert.Equal(serde.Serialize(config1), serde.Serialize(config2));

                DateTime modifiedTime2 = File.GetLastWriteTimeUtc(this.tempFileName);
                Assert.Equal(modifiedTime2, modifiedTime1);

                DeploymentConfigInfo config3 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config3);

                Assert.True(File.Exists(this.tempFileName));
                backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
                returnedJson = serde.Serialize(config3);

                Assert.Equal(backupJson, returnedJson, ignoreCase: true);

                DateTime modifiedTime3 = File.GetLastWriteTimeUtc(this.tempFileName);
                Assert.True(DateTime.UtcNow - modifiedTime1 < TimeSpan.FromSeconds(5));
                Assert.NotEqual(modifiedTime1, modifiedTime3);

                DeploymentConfigInfo config4 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config4);

                Assert.Equal(serde.Serialize(config4), serde.Serialize(config4));

                DateTime modifiedTime4 = File.GetLastWriteTimeUtc(this.tempFileName);
                Assert.Equal(modifiedTime4, modifiedTime3);
            }
        }

        [Fact]
        [Unit]
        public async Task FileBackupReadOnlyWhenUninitialized()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            var underlying = new Mock<IConfigSource>();
            underlying.SetupSequence(t => t.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(ValidConfigInfo1)
                .ReturnsAsync(DeploymentConfigInfo.Empty)
                .ThrowsAsync(new InvalidOperationException())
                .ReturnsAsync(ValidConfigInfo1)
                .ReturnsAsync(DeploymentConfigInfo.Empty)
                .ThrowsAsync(new InvalidOperationException());

            ISerde<DeploymentConfigInfo> serde = this.GetSerde();
            DeploymentConfigInfo config1;
            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, NullEncryptionProvider.Instance))
            {
                config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);

                Assert.True(File.Exists(this.tempFileName));
                string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
                string returnedJson = serde.Serialize(config1);

                Assert.Equal(backupJson, returnedJson, ignoreCase: true);
                File.Delete(this.tempFileName);

                DeploymentConfigInfo config2 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config2);

                Assert.Equal(serde.Serialize(config1), serde.Serialize(config2));

                DeploymentConfigInfo config3 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config3);

                Assert.Equal(serde.Serialize(config1), serde.Serialize(config3));
            }

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, NullEncryptionProvider.Instance))
            {
                config1 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config1);

                Assert.True(File.Exists(this.tempFileName));
                string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
                string returnedJson = serde.Serialize(config1);

                Assert.Equal(backupJson, returnedJson, ignoreCase: true);
            }

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, NullEncryptionProvider.Instance))
            {
                DeploymentConfigInfo config5 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config5);

                Assert.Equal(serde.Serialize(config1), serde.Serialize(config5));
            }

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, NullEncryptionProvider.Instance))
            {
                DeploymentConfigInfo config5 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config5);

                Assert.Equal(serde.Serialize(config1), serde.Serialize(config5));
            }

            File.Delete(this.tempFileName);
            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, underlying.Object, serde, NullEncryptionProvider.Instance))
            {
                DeploymentConfigInfo config6 = await configSource.GetDeploymentConfigInfoAsync();
                Assert.NotNull(config6);

                Assert.Equal(config6, DeploymentConfigInfo.Empty);
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
