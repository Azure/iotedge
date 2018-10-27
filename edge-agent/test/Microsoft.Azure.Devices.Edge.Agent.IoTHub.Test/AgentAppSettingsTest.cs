namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class AgentAppSettingsTest
    {
        [Fact]
        [Unit]
        public void CanReadAppSettings()
        {
            string testDataFile = "TestData_CanReadAppSettings.json";

            try
            {
                string currentFolder = new DirectoryInfo("./").FullName;
                string appSettingsJson = @"
                {
                    ""Mode"": ""docker"",
                    ""DeviceConnectionString"": ""Fake-Device-Connection-String"",
                    ""DockerUri"":""http://localhost:2375"",
                    ""DockerLoggingDriver"": ""json-file"",
                    ""MaxRestartCount"": 20,
                    ""IntensiveCareTimeInMinutes"": 10,
                    ""RuntimeLogLevel"": ""info"",
                    ""StorageFolder"": """ + currentFolder.Replace("\\", "\\\\") + @""",
                    ""UsePersistentStorage"": true,
                    ""ConfigRefreshFrequencySecs"": ""3600""
                }";

                File.WriteAllText(testDataFile, appSettingsJson);

                var appSettings = new AgentAppSettings(testDataFile);

                Assert.Equal(EdgeRuntimeMode.Docker, appSettings.RuntimeMode);
                Assert.Equal("Fake-Device-Connection-String", appSettings.DeviceConnectionString);
                Assert.Equal("json-file", appSettings.DockerLoggingDriver);
                Assert.Equal(20, appSettings.MaxRestartCount);
                Assert.Equal(TimeSpan.FromMinutes(10), appSettings.IntensiveCareTime);
                Assert.Equal(Path.Combine(currentFolder, "edgeAgent"), appSettings.StoragePath);
                Assert.NotNull(appSettings.VersionInfo);
            }
            finally
            {
                File.Delete(testDataFile);
            }

        }

        [Fact]
        [Unit]
        public void ValidatesMaxRestartCountShouldBeGreaterThanOrEqualToOne()
        {
            string testDataFile = "TestData_ValidatesMaxRestartCountShouldBeGreaterThanOrEqualToOne.json";

            try
            {
                bool exceptionThrown = false;
                string appSettingsJson = @"
                {
                    ""Mode"": ""docker"",
                    ""DeviceConnectionString"": ""Fake-Device-Connection-String"",
                    ""DockerUri"":""http://localhost:2375"",
                    ""DockerLoggingDriver"": ""json-file"",
                    ""MaxRestartCount"": 0,
                    ""IntensiveCareTimeInMinutes"": 10,
                    ""RuntimeLogLevel"": ""info"",
                    ""UsePersistentStorage"": true,
                    ""ConfigRefreshFrequencySecs"": ""3600""
                }";

                File.WriteAllText(testDataFile, appSettingsJson);

                try
                {
                    var appSettings = new AgentAppSettings(testDataFile);
                }
                catch (ArgumentOutOfRangeException e)
                {
                    exceptionThrown = true;
                    Assert.Equal("MaxRestartCount", e.ParamName);
                }

                Assert.True(exceptionThrown);
            }
            finally
            {
                File.Delete(testDataFile);
            }
        }

        [Fact]
        [Unit]
        public void ValidateDockerLoggingDriverIsRequired()
        {
            string testDataFile = "TestData_ValidateDockerLoggingDriverIsRequired.json";

            try
            {
                bool exceptionThrown = false;
                string appSettingsJson = @"
                {
                    ""Mode"": ""docker"",
                    ""DeviceConnectionString"": ""Fake-Device-Connection-String"",
                    ""DockerUri"":""http://localhost:2375"",
                    ""MaxRestartCount"": 20,
                    ""IntensiveCareTimeInMinutes"": 10,
                    ""RuntimeLogLevel"": ""info"",
                    ""UsePersistentStorage"": true,
                    ""ConfigRefreshFrequencySecs"": ""3600""
                }";

                File.WriteAllText(testDataFile, appSettingsJson);

                try
                {
                    var appSettings = new AgentAppSettings(testDataFile);
                }
                catch (ArgumentNullException e)
                {
                    exceptionThrown = true;
                    Assert.Equal("DockerLoggingDriver", e.ParamName);
                }

                Assert.True(exceptionThrown);
            }
            finally
            {
                File.Delete(testDataFile);
            }
        }

        [Fact]
        [Unit]
        public void ValidateDockerUriIsRequiredForDockerMode()
        {
            string testDataFile = "TestData_ValidateDockerUriIsRequiredForDockerMode.json";

            try
            {
                bool exceptionThrown = false;
                string appSettingsJson = @"
                {
                    ""Mode"": ""docker"",
                    ""DeviceConnectionString"": ""Fake-Device-Connection-String"",
                    ""DockerLoggingDriver"": ""json-file"",
                    ""MaxRestartCount"": 20,
                    ""IntensiveCareTimeInMinutes"": 10,
                    ""RuntimeLogLevel"": ""info"",
                    ""UsePersistentStorage"": true,
                    ""ConfigRefreshFrequencySecs"": ""3600""
                }";

                File.WriteAllText(testDataFile, appSettingsJson);

                try
                {
                    var appSettings = new AgentAppSettings(testDataFile);
                }
                catch (ArgumentException e)
                {
                    exceptionThrown = true;
                    Assert.Equal("DockerUri", e.ParamName);
                }

                Assert.True(exceptionThrown);
            }
            finally
            {
                File.Delete(testDataFile);
            }
        }

        [Fact]
        [Unit]
        public void ValidateManagementUriIsRequiredForIotEdgedMode()
        {
            string testDataFile = "TestData_ValidateManagementUriIsRequiredForIotEdgedMode.json";

            try
            {
                bool exceptionThrown = false;
                string appSettingsJson = @"
                {
                    ""Mode"": ""iotedged"",
                    ""DeviceConnectionString"": ""Fake-Device-Connection-String"",
                    ""DockerLoggingDriver"": ""json-file"",
                    ""IoTEdge_WorkloadUri"": ""http://localhost:50003"", 
                    ""MaxRestartCount"": 20,
                    ""IntensiveCareTimeInMinutes"": 10,
                    ""RuntimeLogLevel"": ""info"",
                    ""UsePersistentStorage"": true,
                    ""ConfigRefreshFrequencySecs"": ""3600""
                }";

                File.WriteAllText(testDataFile, appSettingsJson);

                try
                {
                    var appSettings = new AgentAppSettings(testDataFile);
                }
                catch (ArgumentException e)
                {
                    exceptionThrown = true;
                    Assert.Equal("ManagementUri", e.ParamName);
                }

                Assert.True(exceptionThrown);
            }
            finally
            {
                File.Delete(testDataFile);
            }
        }

        [Fact]
        [Unit]
        public void ValidateWorkloadUriIsRequiredForIotEdgedMode()
        {
            string testDataFile = "TestData_ValidateWorkloadUriIsRequiredForIotEdgedMode.json";

            try
            {
                bool exceptionThrown = false;
                string appSettingsJson = @"
                {
                    ""Mode"": ""iotedged"",
                    ""DeviceConnectionString"": ""Fake-Device-Connection-String"",
                    ""DockerLoggingDriver"": ""json-file"",
                    ""IoTEdge_ManagementUri"": ""http://localhost:50002"", 
                    ""MaxRestartCount"": 20,
                    ""IntensiveCareTimeInMinutes"": 10,
                    ""RuntimeLogLevel"": ""info"",
                    ""UsePersistentStorage"": true,
                    ""ConfigRefreshFrequencySecs"": ""3600""
                }";

                File.WriteAllText(testDataFile, appSettingsJson);

                try
                {
                    var appSettings = new AgentAppSettings(testDataFile);
                }
                catch (ArgumentException e)
                {
                    exceptionThrown = true;
                    Assert.Equal("WorkloadUri", e.ParamName);
                }

                Assert.True(exceptionThrown);
            }
            finally
            {
                File.Delete(testDataFile);
            }
        }
    }
}
