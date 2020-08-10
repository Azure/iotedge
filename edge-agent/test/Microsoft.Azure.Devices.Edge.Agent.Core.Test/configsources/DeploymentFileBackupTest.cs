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

    public class DeploymentFileBackupTest : IDisposable
    {
        const string TestType = "test";
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly ConfigurationInfo ConfigurationInfo = new ConfigurationInfo();
        static readonly IEdgeAgentModule EdgeAgentModule = new TestAgentModule("edgeAgent", "test", new TestConfig("edge-agent"), ImagePullPolicy.OnCreate, ConfigurationInfo, EnvVars);
        static readonly TestRuntimeInfo TestRuntimeInfo = new TestRuntimeInfo("test");
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly IModule ValidModule1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, ConfigurationInfo, EnvVars);
        static readonly IEdgeHubModule EdgeHubModule = new TestHubModule("edgeHub", "test", ModuleStatus.Running, new TestConfig("edge-hub:latest"), RestartPolicy.Always, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, ConfigurationInfo, EnvVars);
        static readonly IDictionary<string, IModule> Modules1 = new Dictionary<string, IModule> { ["mod1"] = ValidModule1 };
        static readonly IDictionary<string, IModule> Modules2 = new Dictionary<string, IModule> { ["mod2"] = ValidModule1 };
        static readonly DeploymentConfig ValidConfig1 = new DeploymentConfig("1.0", TestRuntimeInfo, new SystemModules(EdgeAgentModule, EdgeHubModule), Modules1);
        static readonly DeploymentConfig ValidConfig2 = new DeploymentConfig("1.0", TestRuntimeInfo, new SystemModules(EdgeAgentModule, EdgeHubModule), Modules2);
        static readonly DeploymentConfigInfo ValidConfigInfo1 = new DeploymentConfigInfo(0, ValidConfig1);
        static readonly DeploymentConfigInfo ValidConfigInfo2 = new DeploymentConfigInfo(0, ValidConfig2);

        readonly string tempFileName;

        public DeploymentFileBackupTest()
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
            IDeploymentBackupSource backupSource = new DeploymentFileBackup(this.tempFileName, this.GetSerde(), NullEncryptionProvider.Instance);
            Assert.NotNull(backupSource);
        }

        [Fact]
        [Unit]
        public void InvalidInputsFails()
        {
            Assert.Throws<ArgumentException>(() => new DeploymentFileBackup(string.Empty, this.GetSerde(), NullEncryptionProvider.Instance));
            Assert.Throws<ArgumentException>(() => new DeploymentFileBackup(null, this.GetSerde(), NullEncryptionProvider.Instance));
            Assert.Throws<ArgumentNullException>(() => new DeploymentFileBackup(this.tempFileName, null, NullEncryptionProvider.Instance));
            Assert.Throws<ArgumentNullException>(() => new DeploymentFileBackup(this.tempFileName, this.GetSerde(), null));
        }

        [Fact]
        [Unit]
        public async void FileBackupSuccessWhenFileNotExists()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            ISerde<DeploymentConfigInfo> serde = this.GetSerde();
            IDeploymentBackupSource fileBackup = new DeploymentFileBackup(this.tempFileName, serde, NullEncryptionProvider.Instance);

            await fileBackup.BackupDeploymentConfigAsync(ValidConfigInfo1);

            Assert.True(File.Exists(this.tempFileName));
            string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
            string expectedJson = serde.Serialize(ValidConfigInfo1);

            Assert.Equal(expectedJson, backupJson, ignoreCase: true);
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
            var exceptionDeployment = new DeploymentConfigInfo(10, new InvalidOperationException());
            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            // Act
            IDeploymentBackupSource fileBackup = new DeploymentFileBackup(this.tempFileName, serde, NullEncryptionProvider.Instance);

            await fileBackup.BackupDeploymentConfigAsync(exceptionDeployment);

            // Assert
            Assert.False(File.Exists(this.tempFileName));
        }

        [Fact]
        [Unit]
        public async void FileBackupReadDoesNotThrowWhenBackupFileDoesNotExist()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            // Arrange
            ISerde<DeploymentConfigInfo> serde = this.GetSerde();

            IDeploymentBackupSource fileBackup = new DeploymentFileBackup(this.tempFileName, serde, NullEncryptionProvider.Instance);

            // Act
            var config = await fileBackup.ReadFromBackupAsync();

            // Assert
            Assert.NotNull(config);
            Assert.Equal(DeploymentConfigInfo.Empty, config);
        }

        [Fact]
        [Unit]
        public async void FileBackupSuccessCallsEncrypt()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            ISerde<DeploymentConfigInfo> serde = this.GetSerde();
            var encryptionProvider = new Mock<IEncryptionProvider>();
            encryptionProvider.Setup(ep => ep.EncryptAsync(It.IsAny<string>()))
                .ReturnsAsync(serde.Serialize(ValidConfigInfo1));

            IDeploymentBackupSource fileBackup = new DeploymentFileBackup(this.tempFileName, serde, encryptionProvider.Object);

            await fileBackup.BackupDeploymentConfigAsync(ValidConfigInfo1);

            Assert.True(File.Exists(this.tempFileName));
            string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
            string expectedJson = serde.Serialize(ValidConfigInfo1);

            Assert.Equal(backupJson, expectedJson, true);
            encryptionProvider.Verify(ep => ep.EncryptAsync(It.IsAny<string>()));
        }

        [Fact]
        [Unit]
        public async void FileBackupDoesnotThrowWhenEncryptFails()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            ISerde<DeploymentConfigInfo> serde = this.GetSerde();
            var encryptionProvider = new Mock<IEncryptionProvider>();
            encryptionProvider.Setup(ep => ep.EncryptAsync(It.IsAny<string>()))
                .ThrowsAsync(new WorkloadCommunicationException("failed", 404));

            IDeploymentBackupSource fileBackup = new DeploymentFileBackup(this.tempFileName, serde, encryptionProvider.Object);

            await fileBackup.BackupDeploymentConfigAsync(ValidConfigInfo1);

            Assert.False(File.Exists(this.tempFileName));
            encryptionProvider.Verify(ep => ep.EncryptAsync(It.IsAny<string>()));
        }

        [Fact]
        [Unit]
        public async void FileBackupReadFromBackupCallsEncryptDecrypt()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            ISerde<DeploymentConfigInfo> serde = this.GetSerde();
            var encryptionProvider = new Mock<IEncryptionProvider>();
            encryptionProvider.Setup(ep => ep.EncryptAsync(It.IsAny<string>()))
                .ReturnsAsync(serde.Serialize(ValidConfigInfo1));
            encryptionProvider.Setup(ep => ep.DecryptAsync(It.IsAny<string>()))
                .ReturnsAsync(serde.Serialize(ValidConfigInfo1));

            IDeploymentBackupSource fileBackup = new DeploymentFileBackup(this.tempFileName, serde, encryptionProvider.Object);

            await fileBackup.BackupDeploymentConfigAsync(ValidConfigInfo1);
            DeploymentConfigInfo config1 = await fileBackup.ReadFromBackupAsync();

            Assert.NotNull(config1);
            Assert.True(File.Exists(this.tempFileName));
            string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
            string returnedJson = serde.Serialize(config1);
            string expectedJson = serde.Serialize(ValidConfigInfo1);

            Assert.Equal(expectedJson, backupJson, ignoreCase: true);
            Assert.Equal(expectedJson, returnedJson, ignoreCase: true);

            encryptionProvider.Verify(ep => ep.EncryptAsync(It.IsAny<string>()));
            encryptionProvider.Verify(ep => ep.DecryptAsync(It.IsAny<string>()));
        }

        [Fact]
        [Unit]
        public async void FileBackupReadThrowsWhenDecryptFails()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            ISerde<DeploymentConfigInfo> serde = this.GetSerde();
            var encryptionProvider = new Mock<IEncryptionProvider>();
            encryptionProvider.Setup(ep => ep.EncryptAsync(It.IsAny<string>()))
                .ReturnsAsync(serde.Serialize(ValidConfigInfo1));
            encryptionProvider.Setup(ep => ep.DecryptAsync(It.IsAny<string>()))
                .ThrowsAsync(new WorkloadCommunicationException("failed", 404));

            IDeploymentBackupSource fileBackup = new DeploymentFileBackup(this.tempFileName, serde, encryptionProvider.Object);

            await fileBackup.BackupDeploymentConfigAsync(ValidConfigInfo1);
            await Assert.ThrowsAsync<WorkloadCommunicationException>(async () => await fileBackup.ReadFromBackupAsync());
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
