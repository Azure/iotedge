// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.ConfigSources
{
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Moq;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class FileBackupConfigSourceTest : IDisposable
    {

        static readonly string validJson1 = "{\"Modules\":{\"mod1\":{\"Name\":\"mod1\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"Config\":{\"Image\":\"image1\"},\"RestartPolicy\":\"on-unhealthy\"},\"mod2\":{\"Name\":\"mod2\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"config\":{\"image\":\"image1\"},\"RestartPolicy\":\"on-unhealthy\"}}}";
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly IModule ValidModule1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1);
        static readonly IModule ValidModule2 = new TestModule("mod2", "version1", "test", ModuleStatus.Running, Config1);
        static readonly ModuleSet ValidSet1 = ModuleSet.Create(ValidModule1, ValidModule2);

        static readonly IModule ValidModule3 = new TestModule("mod3", "version1", "test", ModuleStatus.Running, Config1);
        static readonly Diff DiffAdd = new Diff(new List<IModule>() { ValidModule3 }, ImmutableList<string>.Empty);
        static readonly ModuleSet ValidSetAdd = ModuleSet.Create(ValidModule1, ValidModule2, ValidModule3);

        static readonly Diff DiffRemove = new Diff(ImmutableList<IModule>.Empty, new List<string>() {"mod3"});
        static readonly ModuleSet ValidSetRemove = ModuleSet.Create(ValidModule1, ValidModule2);

        static readonly Diff DiffRemoveAll = new Diff(ImmutableList<IModule>.Empty, new List<string>() { "mod1", "mod2", "mod3" });
        static readonly ModuleSet ValidSetRemoveAll = ModuleSet.Create();

        readonly string tempFileName;
        readonly ModuleSetSerde moduleSetSerde;

        public FileBackupConfigSourceTest()
        {
            this.tempFileName = Path.GetTempFileName();
            var serializerInputTable = new Dictionary<string, Type> { { "Test", typeof(TestModule) } };
            this.moduleSetSerde = new ModuleSetSerde(serializerInputTable);
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
            var config = new Mock<IConfiguration>();

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, this.moduleSetSerde, underlying.Object, config.Object))
            {
                Assert.NotNull(configSource);
            }
        }

        [Fact]
        [Unit]
        public void InvalidInputsFails()
        {
            var underlying = new Mock<IConfigSource>();
            var config = new Mock<IConfiguration>();
            Assert.Throws<ArgumentException>(() => new FileBackupConfigSource("", this.moduleSetSerde, underlying.Object, config.Object));
            Assert.Throws<ArgumentNullException>(() => new FileBackupConfigSource(this.tempFileName, null, underlying.Object, config.Object));
            Assert.Throws<ArgumentNullException>(() => new FileBackupConfigSource(this.tempFileName, this.moduleSetSerde, null, config.Object));
            Assert.Throws<ArgumentNullException>(() => new FileBackupConfigSource(this.tempFileName, this.moduleSetSerde, underlying.Object, null));
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
            underlying.Setup(t => t.GetModuleSetAsync())
                .Returns(Task.FromResult(ValidSet1));
            var config = new Mock<IConfiguration>();

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, this.moduleSetSerde, underlying.Object, config.Object))
            {
                await configSource.GetModuleSetAsync();
                Assert.True(File.Exists(this.tempFileName));
            }

            string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
            string returnedJson = this.moduleSetSerde.Serialize(ValidSet1);

            Assert.True(string.Equals(backupJson, returnedJson, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        [Unit]
        public async void RestoringFromBackupThrowsExceptionWhenFileNotExists()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            var underlying = new Mock<IConfigSource>();
            underlying.Setup(t => t.GetModuleSetAsync())
                .Throws<Exception>();
            var config = new Mock<IConfiguration>();

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, this.moduleSetSerde, underlying.Object, config.Object))
            {
               await Assert.ThrowsAsync<FileNotFoundException>(async () => await configSource.GetModuleSetAsync());
            }
        }

        [Fact]
        [Unit]
        public void ApplyDiffFailsWhenFileNotExists()
        {
            if (File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }

            var underlying = new Mock<IConfigSource>();
            var config = new Mock<IConfiguration>();

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, this.moduleSetSerde, underlying.Object, config.Object))
            {
                bool failureHandlerCalled = false;
                configSource.ModuleSetFailed += (sender, exception) => failureHandlerCalled = true;
                underlying.Raise(t => t.ModuleSetChanged += null, null, null);
                Assert.True(failureHandlerCalled == true);
            }
        }

        [Fact]
        [Unit]
        public async void SetAndGetModuleSetSuccess()
        {
            File.WriteAllText(this.tempFileName, validJson1);

            var underlying = new Mock<IConfigSource>();
            underlying.Setup(t => t.GetModuleSetAsync())
                .Throws<Exception>();
            var config = new Mock<IConfiguration>();

            using (FileBackupConfigSource configSource = new FileBackupConfigSource(this.tempFileName, this.moduleSetSerde, underlying.Object, config.Object) as FileBackupConfigSource)
            {
                ModuleSet backup = await configSource.GetModuleSetAsync();
                string actualJson = this.moduleSetSerde.Serialize(backup);

                Assert.True(ValidSet1.Diff(backup).IsEmpty);
            }
        }

        [Fact]
        [Unit]
        public async void AddModuleToBackupSuccess()
        {
            var underlying = new Mock<IConfigSource>();
            underlying.Setup(t => t.GetModuleSetAsync())
                .Returns(Task.FromResult(ValidSet1));
            var config = new Mock<IConfiguration>();

            using (FileBackupConfigSource configSource = new FileBackupConfigSource(this.tempFileName, this.moduleSetSerde, underlying.Object, config.Object) as FileBackupConfigSource)
            {
                ModuleSet backup = await configSource.GetModuleSetAsync();

                underlying.Setup(t => t.GetModuleSetAsync())
                    .Throws<Exception>();
                
                // add a module
                underlying.Raise(t => t.ModuleSetChanged += null, null, DiffAdd);
                ModuleSet updated = await configSource.GetModuleSetAsync();
                Assert.True(updated.Diff(ValidSetAdd).IsEmpty);

                // remove a module
                underlying.Raise(t => t.ModuleSetChanged += null, null, DiffRemove);
                updated = await configSource.GetModuleSetAsync();
                Assert.True(updated.Diff(ValidSetRemove).IsEmpty);

                // remove all modules
                underlying.Raise(t => t.ModuleSetChanged += null, null, DiffRemoveAll);
                updated = await configSource.GetModuleSetAsync();
                Assert.True(updated.Diff(ValidSetRemoveAll).IsEmpty);

                // add modules back
                underlying.Raise(t => t.ModuleSetChanged += null, null, DiffAdd);
                updated = await configSource.GetModuleSetAsync();
                Assert.True(updated.Diff(ModuleSet.Create(ValidModule3)).IsEmpty);
            }
        }

        [Fact]
        [Unit]
        public async void BackupFailureThrowsCustomException()
        {
            var underlying = new Mock<IConfigSource>();
            underlying.Setup(t => t.GetModuleSetAsync())
                .Returns(Task.FromResult(ValidSet1));
            var config = new Mock<IConfiguration>();
            var moduleSetSerde = new Mock<ISerde<ModuleSet>>();
            moduleSetSerde.Setup(t => t.Serialize(It.IsAny<ModuleSet>()))
                .Throws<Exception>();

            using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName,
                moduleSetSerde.Object, underlying.Object, config.Object))
            {
                await Assert.ThrowsAsync<FileBackupException>(async () => await configSource.GetModuleSetAsync());
            }
        }
    }
}
