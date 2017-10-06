// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test.Reporters
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Reporters;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public class IoTHubReporterTest
    {
        [Fact]
        [Unit]
        public void CreateInvalidInputs()
        {
            // Arrange
            var deviceClient = new Mock<IDeviceClient>();
            var twinConfigSource = new Mock<ITwinConfigSource>();

            // Act
            // Assert
            Assert.Throws<ArgumentNullException>(() => new IoTHubReporter(null, twinConfigSource.Object));
            Assert.Throws<ArgumentNullException>(() => new IoTHubReporter(deviceClient.Object, null));
        }

        [Fact]
        [Unit]
        public async void ReportedPatchTest()
        {
            // Arrange
            var deviceClient = new Mock<IDeviceClient>();
            var twinConfigSource = new Mock<ITwinConfigSource>();
            var reporter = new IoTHubReporter(deviceClient.Object, twinConfigSource.Object);
            TwinCollection patch = null;

            var reportedModuleSet = ModuleSet.Create(
                new TestRuntimeModule(
                    "mod1", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                    new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                    0, DateTime.MinValue, ModuleStatus.Running
                ),
                new TestRuntimeModule(
                    "extra_mod", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                    new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                    0, DateTime.MinValue, ModuleStatus.Backoff
                )
            );
            var currentModuleSet = ModuleSet.Create(
                new TestRuntimeModule(
                    "mod1", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                    new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                    0, DateTime.MinValue, ModuleStatus.Backoff
                ),
                new TestRuntimeModule(
                    "mod2", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                    new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                    0, DateTime.MinValue, ModuleStatus.Running
                )
            );
            twinConfigSource.SetupGet(tcs => tcs.ReportedModuleSet)
                .Returns(reportedModuleSet);
            deviceClient.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .Callback<TwinCollection>(tc => patch = tc)
                .Returns(Task.CompletedTask);

            // Act
            await reporter.ReportAsync(currentModuleSet);

            // Assert
            twinConfigSource.VerifyAll();
            Assert.NotNull(patch);

            JObject json = JsonConvert.DeserializeObject(patch.ToJson()) as JObject;
            JObject expectedJson = JsonConvert.DeserializeObject(
                "{" +
                    "\"modules\": {" +
                        $"\"mod1\": {JsonConvert.SerializeObject(currentModuleSet.Modules["mod1"])}," +
                        $"\"mod2\": {JsonConvert.SerializeObject(currentModuleSet.Modules["mod2"])}," +
                        "\"extra_mod\": null" +
                    "}" +
                "}"
            ) as JObject;
            Assert.True(JToken.DeepEquals(expectedJson, json));
        }

        [Fact]
        [Unit]
        public async void ReportedPatchTest2()
        {
            // Arrange
            var deviceClient = new Mock<IDeviceClient>();
            var twinConfigSource = new Mock<ITwinConfigSource>();
            var reporter = new IoTHubReporter(deviceClient.Object, twinConfigSource.Object);
            TwinCollection patch = null;

            var reportedModuleSet = ModuleSet.Create(
                new TestRuntimeModule(
                    "mod1", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                    new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                    0, DateTime.MinValue, ModuleStatus.Running
                ),
                new TestRuntimeModule(
                    "extra_mod", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                    new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                    0, DateTime.MinValue, ModuleStatus.Backoff
                )
            );
            var currentModuleSet = ModuleSet.Create(
                new TestRuntimeModule(
                    "mod1", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                    new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                    0, DateTime.MinValue, ModuleStatus.Backoff
                ),
                new TestRuntimeModule(
                    "mod2", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                    new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                    0, DateTime.MinValue, ModuleStatus.Running
                )
            );
            twinConfigSource.SetupGet(tcs => tcs.ReportedModuleSet)
                .Returns(reportedModuleSet);
            deviceClient.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .Callback<TwinCollection>(tc => patch = tc)
                .Returns(Task.CompletedTask);

            // Act

            // this should cause "extra_mod" to get deleted and "mod1" and "mod2" to get updated
            await reporter.ReportAsync(currentModuleSet);

            // now change "current" so that "mod1" fails
            reportedModuleSet = currentModuleSet;
            currentModuleSet = ModuleSet.Create(
                new TestRuntimeModule(
                    "mod1", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                    new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                    0, DateTime.MinValue, ModuleStatus.Failed
                ),
                new TestRuntimeModule(
                    "mod2", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                    new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                    0, DateTime.MinValue, ModuleStatus.Running
                )
            );

            // this should "mod1" to get updated
            await reporter.ReportAsync(currentModuleSet);

            // Assert
            twinConfigSource.VerifyAll();
            Assert.NotNull(patch);

            JObject json = JsonConvert.DeserializeObject(patch.ToJson()) as JObject;
            JObject expectedJson = JsonConvert.DeserializeObject(
                "{" +
                    "\"modules\": {" +
                        $"\"mod1\": {JsonConvert.SerializeObject(currentModuleSet.Modules["mod1"])}" +
                    "}" +
                "}"
            ) as JObject;
            Assert.True(JToken.DeepEquals(expectedJson, json));
        }
    }
}
