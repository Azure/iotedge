namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System.IO;
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

                Assert.Equal(Path.Combine(currentFolder, "edgeAgent"), appSettings.StoragePath);
            }
            finally
            {
                File.Delete(testDataFile);
            }

        }
    }
}
