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
    using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using Xunit;

    public class FileBackupConfigSourceTest : IDisposable
    {

        static readonly string validJson1 = "{\"Modules\":{\"mod1\":{\"Name\":\"mod1\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"Config\":{\"Image\":\"image1\"}},\"mod2\":{\"Name\":\"mod2\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"config\":{\"image\":\"image1\"}}}}";
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly IModule ValidModule1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1);
        static readonly IModule ValidModule2 = new TestModule("mod2", "version1", "test", ModuleStatus.Running, Config1);
        static readonly ModuleSet ValidSet1 = ModuleSet.Create(ValidModule1, ValidModule2);

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
            File.Delete(this.tempFileName);

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
		public async void FileBackupUpdatedSuccess()
		{
			File.Delete(this.tempFileName);

			var underlying = new Mock<IConfigSource>();
			var diff = new Mock<Diff>();
			var config = new Mock<IConfiguration>();

			using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, this.moduleSetSerde, underlying.Object, config.Object))
			{
				underlying.Raise(t => t.ModuleSetChanged += null, new ModuleSetChangedArgs(diff.Object, ValidSet1));

				while (!File.Exists(this.tempFileName))
				{
					await Task.Delay(1000);
				}
			}

			// Retry because the FileBackupConfigSource could be updating the file in the background
			for (int i = 0; i < 10; i++)
			{
				try
				{
					string backupJson = await DiskFile.ReadAllAsync(this.tempFileName);
					string returnedJson = this.moduleSetSerde.Serialize(ValidSet1);

					Assert.True(string.Equals(backupJson, returnedJson, StringComparison.OrdinalIgnoreCase));
					break;
				}
				catch
				{
					await Task.Delay(1000);
				}
			}
		}

        [Fact]
        [Unit]
        public async void GetModuleSetFailsWhenFileNotExists()
        {
            File.Delete(this.tempFileName);

			var underlying = new Mock<IConfigSource>();
			underlying.Setup(t => t.GetModuleSetAsync())
				.Throws<Exception>();
			var config = new Mock<IConfiguration>();

			using (IConfigSource configSource = new FileBackupConfigSource(this.tempFileName, this.moduleSetSerde, underlying.Object, config.Object))
            {
				bool failureHandlerCalled = false;

				configSource.ModuleSetFailed += (sender, exception) => failureHandlerCalled = true;

                await Assert.ThrowsAsync<FileNotFoundException>(async () => await configSource.GetModuleSetAsync());
				Assert.True(failureHandlerCalled == true);
			}
        }

		private void ConfigSource_ModuleSetFailed(object sender, Exception e)
		{
			throw new NotImplementedException();
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
                await configSource.BackupModuleSet(ValidSet1);
                ModuleSet backup = await configSource.GetModuleSetAsync();
                string actualJson = this.moduleSetSerde.Serialize(backup);

				Assert.True(ValidSet1.Diff(backup).IsEmpty);
            }
        }
	}
}
