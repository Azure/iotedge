// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.ConfigSources
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    public class FileConfigSourceTest : IDisposable
    {

        static readonly string validJson1 = "{\"Modules\":{\"mod1\":{\"Name\":\"mod1\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"Config\":{\"Image\":\"image1\"}},\"mod2\":{\"Name\":\"mod2\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"config\":{\"image\":\"image1\"}}}}";
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly IModule ValidModule1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1);
        static readonly IModule ValidModule2 = new TestModule("mod2", "version1", "test", ModuleStatus.Running, Config1);
        static readonly ModuleSet ValidSet1 = ModuleSet.Create(ValidModule1, ValidModule2);

        static readonly string validJson2 = "{\"Modules\":{\"mod1\":{\"Name\":\"mod1\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Stopped\",\"Config\":{\"Image\":\"image1\"}},\"mod3\":{\"Name\":\"mod3\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"config\":{\"image\":\"image1\"}}}}";
        static readonly IModule UpdatedModule1 = new TestModule("mod1", "version1", "test", ModuleStatus.Stopped, Config1);
        static readonly IModule ValidModule3 = new TestModule("mod3", "version1", "test", ModuleStatus.Running, Config1);
        static readonly ModuleSet ValidSet2 = ModuleSet.Create(UpdatedModule1, ValidModule3);

        static readonly string InvalidJson1 = "{\"This is a terrible string\"}";

        readonly string tempFileName;
        readonly ModuleSetSerde moduleSetSerde;

        public FileConfigSourceTest()
        {
            // GetTempFileName() creates the file.
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
        public async void CreateSuccess()
        {
            File.WriteAllText(this.tempFileName, validJson1);

            using (FileConfigSource configSource = await FileConfigSource.Create(this.tempFileName, this.moduleSetSerde))
            {
                Assert.NotNull(configSource);
                ModuleSet configSourceSet = await configSource.GetModuleSetAsync();
                Assert.NotNull(configSourceSet);
                Diff emptyDiff = ValidSet1.Diff(configSourceSet);
                Assert.True(emptyDiff.IsEmpty);
            }
        }

        [Fact]
        [Unit]
        public async void ChangeFileAndSeeChange()
        {
            // Set up initial config file and create `FileConfigSource`
            File.WriteAllText(this.tempFileName, validJson1);
            Diff validDiff1To2 = ValidSet2.Diff(ValidSet1);

            using (FileConfigSource configSource = await FileConfigSource.Create(this.tempFileName, this.moduleSetSerde))
            {
                Assert.NotNull(configSource);

                var taskComplete = new TaskCompletionSource<bool>();
                bool eventChangeCalled = false;
                Diff eventDiff = Diff.Empty;

                configSource.Changed += (sender, diff) =>
                {
                    eventDiff = diff;
                    eventChangeCalled = true;
                    taskComplete.SetResult(true);
                };

                bool eventFailedCalled = false;
                configSource.Failed += (sender, ex) =>
                {
                    eventFailedCalled = true;
                };

                ModuleSet startingSet = await configSource.GetModuleSetAsync();

                // Modify the config file by writing new content.
                File.WriteAllText(this.tempFileName, validJson2);

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await Task.WhenAny(taskComplete.Task, cts.Token.WhenCanceled());

                // Assert change event is invoked, and the diff from the event is expected.
                Assert.True(eventChangeCalled);
                Assert.False(eventFailedCalled);
                Assert.Equal(eventDiff, validDiff1To2);

                // Assert new read from config file is as expected.
                ModuleSet configSourceSet = await configSource.GetModuleSetAsync();
                Assert.NotNull(configSourceSet);
                Diff configDiff = configSourceSet.Diff(startingSet);
                Assert.Equal(configDiff, validDiff1To2); 
            }
        }

        [Fact]
        [Unit]
        public async void ConstructorInvalidInputs()
        {
            File.Delete(this.tempFileName);

            await Assert.ThrowsAsync<ArgumentNullException>( () => FileConfigSource.Create(null, this.moduleSetSerde));
            await Assert.ThrowsAsync<FileNotFoundException>( () => FileConfigSource.Create(this.tempFileName, this.moduleSetSerde));

            File.WriteAllText(this.tempFileName, validJson1);

            await Assert.ThrowsAsync<ArgumentNullException>( () => FileConfigSource.Create(this.tempFileName, null));
        }

        [Fact]
        [Unit]
        public async void SerializationOnInitFails()
        {
            File.WriteAllText(this.tempFileName, InvalidJson1);

            await Assert.ThrowsAnyAsync<JsonException>( () => FileConfigSource.Create(this.tempFileName, this.moduleSetSerde));
        }

        [Fact]
        [Unit]
        public async void ChangeFileToInvalidAndSeeNoChange()
        {
            // Set up initial config file and create `FileConfigSource`
            File.WriteAllText(this.tempFileName, validJson1);

            using (FileConfigSource configSource = await FileConfigSource.Create(this.tempFileName, this.moduleSetSerde))
            {
                Assert.NotNull(configSource);

                // This event is created to ensure it is *not* invoked on invalid input.
                var taskCompleted = new TaskCompletionSource<bool>();
                bool changeEventCalled = false;
                configSource.Changed += (sender, diff) =>
                {
                    changeEventCalled = true;
                    taskCompleted.SetResult(true);
                };

                bool eventFailedCalled = false;
                configSource.Failed += (sender, ex) =>
                {
                    eventFailedCalled = true;
                };
                // Attempt to modify the config with invalid input.
                File.WriteAllText(this.tempFileName, InvalidJson1);

                var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));
                cts.Token.ThrowIfCancellationRequested();
                await Task.WhenAny(taskCompleted.Task, cts.Token.WhenCanceled());

                // Assert that event is not invoked. 
                Assert.False(changeEventCalled);
                Assert.True(eventFailedCalled);

                // Assert that a read from invalid JSON will fail.
                await Assert.ThrowsAnyAsync<JsonException>(() => FileConfigSource.Create(this.tempFileName, this.moduleSetSerde));

            }
        }

        [Fact]
        [Unit]
        public async void ChangeFileToInvalidBackToOk()
        {
            File.WriteAllText(this.tempFileName, validJson1);

            using (FileConfigSource configSource = await FileConfigSource.Create(this.tempFileName, this.moduleSetSerde))
            {
                Assert.NotNull(configSource);

                ModuleSet startingSet = await configSource.GetModuleSetAsync();

                // watching the file in this test to cover the case where no events are invoked.
                var watcher = new FileSystemWatcher(Path.GetDirectoryName(this.tempFileName), Path.GetFileName(this.tempFileName))
                {
                    NotifyFilter = NotifyFilters.LastWrite
                };
                watcher.EnableRaisingEvents = true;

                File.WriteAllText(this.tempFileName, InvalidJson1);

                watcher.WaitForChanged(WatcherChangeTypes.Changed, 1000);
                await Assert.ThrowsAnyAsync<JsonException>(() => FileConfigSource.Create(this.tempFileName, this.moduleSetSerde));

                File.WriteAllText(this.tempFileName, validJson1);

                watcher.WaitForChanged(WatcherChangeTypes.Changed, 1000);

                ModuleSet configSourceSet = await configSource.GetModuleSetAsync();
                Assert.NotNull(configSourceSet);

                Diff configDiff = startingSet.Diff(configSourceSet);
                Assert.True(configDiff.IsEmpty);
            }
        }
    }

}
