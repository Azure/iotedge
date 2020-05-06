// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.ConfigSources
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;
    using Xunit.Sdk;

    [Unit]
    public class BackupConfigSourceTest
    {
        [Fact]
        public void NullOptionsThrowsExceptions()
        {
            var underlying = Mock.Of<IConfigSource>();
            var backup = Mock.Of<IDeploymentBackupSource>();
            Assert.Throws<ArgumentNullException>(() => new BackupConfigSource(null, underlying));
            Assert.Throws<ArgumentNullException>(() => new BackupConfigSource(backup, null));
        }

        [Fact]
        public async void GetsConfigFromUnderlyingAndBacksUp()
        {
            DeploymentConfigInfo configInfo = SetupNonEmptyDeployment();

            var underlying = new Mock<IConfigSource>();
            underlying.Setup(u => u.GetDeploymentConfigInfoAsync()).ReturnsAsync(configInfo);
            var backup = new Mock<IDeploymentBackupSource>();
            backup.SetupGet(b => b.Name).Returns("backup");
            backup.Setup(b => b.BackupDeploymentConfigAsync(configInfo)).Returns(TaskEx.Done);

            var backupSource = new BackupConfigSource(backup.Object, underlying.Object);
            var result1 = await backupSource.GetDeploymentConfigInfoAsync();
            var result2 = await backupSource.GetDeploymentConfigInfoAsync();
            Assert.Equal(configInfo, result1);
            Assert.Equal(configInfo, result2);
            underlying.VerifyAll();
            backup.Verify(b => b.BackupDeploymentConfigAsync(configInfo), Times.Once);
        }

        [Fact]
        public async void ConfigBacksUpEachTimeItChanges()
        {
            DeploymentConfigInfo configInfo1 = SetupNonEmptyDeployment();
            DeploymentConfigInfo configInfo2 = SetupNonEmptyDeployment("new-module");

            var underlying = new Mock<IConfigSource>();
            underlying.SetupSequence(u => u.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(configInfo1)
                .ReturnsAsync(configInfo2);
            var backup = new Mock<IDeploymentBackupSource>();
            backup.SetupGet(b => b.Name).Returns("backup");
            backup.Setup(b => b.BackupDeploymentConfigAsync(configInfo1)).Returns(TaskEx.Done);
            backup.Setup(b => b.BackupDeploymentConfigAsync(configInfo2)).Returns(TaskEx.Done);

            var backupSource = new BackupConfigSource(backup.Object, underlying.Object);
            var result1 = await backupSource.GetDeploymentConfigInfoAsync();
            var result2 = await backupSource.GetDeploymentConfigInfoAsync();
            Assert.Equal(configInfo1, result1);
            Assert.Equal(configInfo2, result2);
            underlying.VerifyAll();
            backup.Verify(b => b.BackupDeploymentConfigAsync(configInfo1), Times.Once);
            backup.Verify(b => b.BackupDeploymentConfigAsync(configInfo2), Times.Once);
        }

        [Fact]
        public async void ConfigsWithExceptionsDoNotBackUp()
        {
            DeploymentConfigInfo configInfo = SetupExceptionDeployment();
            var underlying = new Mock<IConfigSource>();
            underlying.Setup(u => u.GetDeploymentConfigInfoAsync()).ReturnsAsync(configInfo);
            var backup = new Mock<IDeploymentBackupSource>();
            backup.SetupGet(b => b.Name).Returns("backup");

            var backupSource = new BackupConfigSource(backup.Object, underlying.Object);
            var result1 = await backupSource.GetDeploymentConfigInfoAsync();
            Assert.Equal(configInfo, result1);
            underlying.VerifyAll();
            backup.VerifyAll();
        }

        [Fact]
        public async void EmptyResultFromUnderlyingReadsBackup()
        {
            DeploymentConfigInfo configInfo = SetupNonEmptyDeployment();

            var underlying = new Mock<IConfigSource>();
            underlying.Setup(u => u.GetDeploymentConfigInfoAsync()).ReturnsAsync(DeploymentConfigInfo.Empty);
            var backup = new Mock<IDeploymentBackupSource>();
            backup.SetupGet(b => b.Name).Returns("backup");
            backup.Setup(b => b.ReadFromBackupAsync()).ReturnsAsync(configInfo);

            var backupSource = new BackupConfigSource(backup.Object, underlying.Object);
            var result = await backupSource.GetDeploymentConfigInfoAsync();
            Assert.Equal(configInfo, result);
            underlying.VerifyAll();
            backup.VerifyAll();
        }

        [Fact]
        public async void ExceptionFromUnderlyingReadsBackup()
        {
            DeploymentConfigInfo configInfo = SetupNonEmptyDeployment();

            var underlying = new Mock<IConfigSource>();
            underlying.Setup(u => u.GetDeploymentConfigInfoAsync()).ThrowsAsync(new NullException("failure"));
            var backup = new Mock<IDeploymentBackupSource>();
            backup.SetupGet(b => b.Name).Returns("backup");
            backup.Setup(b => b.ReadFromBackupAsync()).ReturnsAsync(configInfo);

            var backupSource = new BackupConfigSource(backup.Object, underlying.Object);
            var result = await backupSource.GetDeploymentConfigInfoAsync();
            Assert.Equal(configInfo, result);
            underlying.VerifyAll();
            backup.VerifyAll();
        }

        [Fact]
        public async void ReadsBackupOnce()
        {
            DeploymentConfigInfo configInfo = SetupNonEmptyDeployment();

            var underlying = new Mock<IConfigSource>();
            underlying.Setup(u => u.GetDeploymentConfigInfoAsync()).ThrowsAsync(new NullException("failure"));
            var backup = new Mock<IDeploymentBackupSource>();
            backup.SetupGet(b => b.Name).Returns("backup");
            backup.SetupSequence(b => b.ReadFromBackupAsync())
                .ReturnsAsync(configInfo)
                .ReturnsAsync(DeploymentConfigInfo.Empty);

            var backupSource = new BackupConfigSource(backup.Object, underlying.Object);
            var result1 = await backupSource.GetDeploymentConfigInfoAsync();
            var result2 = await backupSource.GetDeploymentConfigInfoAsync();
            Assert.Equal(configInfo, result1);
            Assert.Equal(configInfo, result2);

            underlying.VerifyAll();
            backup.Verify(b => b.ReadFromBackupAsync(), Times.Once);
        }

        [Fact]
        public async void ReadingBackupFailureGivesEmpty()
        {
            var underlying = new Mock<IConfigSource>();
            underlying.Setup(u => u.GetDeploymentConfigInfoAsync()).ThrowsAsync(new NullException("underlying"));
            var backup = new Mock<IDeploymentBackupSource>();
            backup.SetupGet(b => b.Name).Returns("backup");
            backup.Setup(b => b.ReadFromBackupAsync()).ThrowsAsync(new NullException("backup"));

            var backupSource = new BackupConfigSource(backup.Object, underlying.Object);
            var result = await backupSource.GetDeploymentConfigInfoAsync();
            Assert.Equal(DeploymentConfigInfo.Empty, result);
            underlying.VerifyAll();
            backup.VerifyAll();
        }

        public static DeploymentConfigInfo SetupNonEmptyDeployment(string moduleName = "m1")
        {
            var runtime = Mock.Of<IRuntimeInfo>();
            var m1 = Mock.Of<IModule>();
            var modules = new Dictionary<string, IModule> { [moduleName] = m1 };
            SystemModules systemMods = new SystemModules(Option.None<IEdgeAgentModule>(), Option.None<IEdgeHubModule>());
            return new DeploymentConfigInfo(1, new DeploymentConfig("1.0", runtime, systemMods, modules));
        }

        public static DeploymentConfigInfo SetupExceptionDeployment()
        {
            return new DeploymentConfigInfo(2, new NullException("bad config"));
        }
    }
}
