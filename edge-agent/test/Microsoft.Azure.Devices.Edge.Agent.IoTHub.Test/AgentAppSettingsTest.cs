// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
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
                    ""ConfigRefreshFrequencySecs"": ""3600"",
                    ""https_proxy"":""http://proxyserver:1234""
                }";

                File.WriteAllText(testDataFile, appSettingsJson);

                IConfigurationRoot config = new ConfigurationBuilder()
                    .AddJsonFile(testDataFile)
                    .Build();
                var appSettings = new AgentAppSettings(config);

                Assert.Equal(EdgeRuntimeMode.Docker, appSettings.RuntimeMode);
                Assert.Equal("Fake-Device-Connection-String", appSettings.DeviceConnectionString);
                Assert.Equal("json-file", appSettings.DockerLoggingDriver);
                Assert.Equal(20, appSettings.MaxRestartCount);
                Assert.Equal(TimeSpan.FromMinutes(10), appSettings.IntensiveCareTime);
                Assert.Equal(Path.Combine(currentFolder, "edgeAgent"), appSettings.StoragePath);
                Assert.True(appSettings.UsePersistentStorage);
                Assert.Equal(TimeSpan.FromSeconds(3600), appSettings.ConfigRefreshFrequency);
                Assert.Equal("http://proxyserver:1234", appSettings.HttpsProxy.Match(p => ((WebProxy)p).Address.OriginalString, () => string.Empty));
                Assert.NotNull(appSettings.VersionInfo);
            }
            finally
            {
                File.Delete(testDataFile);
            }
        }

        [Fact]
        [Unit]
        public void ValidateAppSettingsAreCaseInsensitive()
        {
            string testDataFile = "TestData_AppSettingsAreCaseInsensitive.json";

            try
            {
                string currentFolder = new DirectoryInfo("./").FullName;
                string appSettingsJson = @"
                {
                    ""MODE"": ""DOCKER"",
                    ""DEVICEConnectionString"": ""Fake-Device-Connection-String"",
                    ""DockerUri"":""http://localhost:2375"",
                    ""dOcKeRLoGgInGDrIvEr"": ""json-file"",
                    ""MaxRestartCount"": 20,
                    ""IntensiveCareTimeInMinutes"": 10,
                    ""RuntimeLogLevel"": ""info"",
                    ""StorageFolder"": """ + currentFolder.Replace("\\", "\\\\") + @""",
                    ""UsePersistentStorage"": true,
                    ""ConfigRefreshFrequencySecs"": ""3600""
                }";

                File.WriteAllText(testDataFile, appSettingsJson);

                IConfigurationRoot config = new ConfigurationBuilder()
                    .AddJsonFile(testDataFile)
                    .Build();
                var appSettings = new AgentAppSettings(config);

                Assert.Equal(EdgeRuntimeMode.Docker, appSettings.RuntimeMode);
                Assert.Equal("Fake-Device-Connection-String", appSettings.DeviceConnectionString);
                Assert.Equal("json-file", appSettings.DockerLoggingDriver);
            }
            finally
            {
                File.Delete(testDataFile);
            }
        }

        [Fact]
        [Unit]
        public void ValidateMaxRestartCountShouldBeGreaterThanOrEqualToOne()
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
                    IConfigurationRoot config = new ConfigurationBuilder()
                        .AddJsonFile(testDataFile)
                        .Build();
                    var appSettings = new AgentAppSettings(config);
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
                    IConfigurationRoot config = new ConfigurationBuilder()
                        .AddJsonFile(testDataFile)
                        .Build();
                    var appSettings = new AgentAppSettings(config);
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
                    IConfigurationRoot config = new ConfigurationBuilder()
                        .AddJsonFile(testDataFile)
                        .Build();
                    var appSettings = new AgentAppSettings(config);
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
                    IConfigurationRoot config = new ConfigurationBuilder()
                        .AddJsonFile(testDataFile)
                        .Build();
                    var appSettings = new AgentAppSettings(config);
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
                    IConfigurationRoot config = new ConfigurationBuilder()
                        .AddJsonFile(testDataFile)
                        .Build();
                    var appSettings = new AgentAppSettings(config);
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
