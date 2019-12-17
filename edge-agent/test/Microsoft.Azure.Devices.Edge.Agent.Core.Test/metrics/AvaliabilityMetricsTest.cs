// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using App.Metrics;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    [Unit]
    public class AvailabilityMetricsTest
    {
        static readonly TestConfig Config = new TestConfig("image1");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");

        [Fact]
        public void ReportsAccurateAvailability()
        {
            /* Setup */
            Dictionary<string, double> runningTime = new Dictionary<string, double>();
            Dictionary<string, double> expectedTime = new Dictionary<string, double>();
            Action<double, string[]> OnSet(Dictionary<string, double> result)
            {
                return (val, tags) =>
                    {
                        if (tags[0] == "edgeAgent")
                        {
                            return; // Ignore edgeAgent b/c it is calculated differently
                        }

                        result[tags[0]] = val;
                    };
            }

            var metricsProvider = new Mock<IMetricsProvider>();

            var runningTimeGauge = new Mock<IMetricsGauge>();
            runningTimeGauge.Setup(x => x.Set(It.IsAny<double>(), It.IsAny<string[]>())).Callback(OnSet(runningTime));
            metricsProvider.Setup(x => x.CreateGauge(
                    "total_time_running_correctly_seconds",
                    It.IsAny<string>(),
                    new List<string> { "module_name" }))
                .Returns(runningTimeGauge.Object);

            var expectedTimeGauge = new Mock<IMetricsGauge>();
            expectedTimeGauge.Setup(x => x.Set(It.IsAny<double>(), It.IsAny<string[]>())).Callback(OnSet(expectedTime));
            metricsProvider.Setup(x => x.CreateGauge(
                    "total_time_expected_running_seconds",
                    It.IsAny<string>(),
                    new List<string> { "module_name" }))
                .Returns(expectedTimeGauge.Object);

            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = DateTime.Now;
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            AvailabilityMetrics availabilityMetrics = new AvailabilityMetrics(metricsProvider.Object, Path.GetTempPath(), systemTime.Object);

            (TestRuntimeModule[] current, TestModule[] desired) = GetTestModules(3);
            ModuleSet currentModuleSet = ModuleSet.Create(current as IModule[]);
            ModuleSet desiredModuleSet = ModuleSet.Create(desired);

            /* Test */
            availabilityMetrics.ComputeAvailability(desiredModuleSet, currentModuleSet);
            Assert.Empty(runningTime);

            fakeTime = fakeTime.AddMinutes(10);
            availabilityMetrics.ComputeAvailability(desiredModuleSet, currentModuleSet);
            Assert.Equal(3, runningTime.Count);
            Assert.Equal(3, expectedTime.Count);
            Assert.Equal(600, runningTime[current[0].Name]);
            Assert.Equal(600, expectedTime[current[0].Name]);
            Assert.Equal(600, runningTime[current[1].Name]);
            Assert.Equal(600, expectedTime[current[1].Name]);
            Assert.Equal(600, runningTime[current[2].Name]);
            Assert.Equal(600, expectedTime[current[2].Name]);

            fakeTime = fakeTime.AddMinutes(10);
            current[1].RuntimeStatus = ModuleStatus.Failed;
            current[2].RuntimeStatus = ModuleStatus.Failed;
            availabilityMetrics.ComputeAvailability(desiredModuleSet, currentModuleSet);
            Assert.Equal(3, runningTime.Count);
            Assert.Equal(3, expectedTime.Count);
            Assert.Equal(1200, runningTime[current[0].Name]);
            Assert.Equal(1200, expectedTime[current[0].Name]);
            Assert.Equal(600, runningTime[current[1].Name]);
            Assert.Equal(1200, expectedTime[current[1].Name]);
            Assert.Equal(600, runningTime[current[2].Name]);
            Assert.Equal(1200, expectedTime[current[2].Name]);

            fakeTime = fakeTime.AddMinutes(10);
            current[1].RuntimeStatus = ModuleStatus.Running;
            availabilityMetrics.ComputeAvailability(desiredModuleSet, currentModuleSet);
            Assert.Equal(3, runningTime.Count);
            Assert.Equal(3, expectedTime.Count);
            Assert.Equal(1800, runningTime[current[0].Name]);
            Assert.Equal(1800, expectedTime[current[0].Name]);
            Assert.Equal(1200, runningTime[current[1].Name]);
            Assert.Equal(1800, expectedTime[current[1].Name]);
            Assert.Equal(600, runningTime[current[2].Name]);
            Assert.Equal(1800, expectedTime[current[2].Name]);
        }

        static (TestRuntimeModule[], TestModule[]) GetTestModules(int num)
        {
            List<TestRuntimeModule> current = new List<TestRuntimeModule>();
            List<TestModule> desired = new List<TestModule>();

            for (int i = 0; i < num; i++)
            {
                (TestRuntimeModule curr, TestModule des) = GetTestModulePair();
                current.Add(curr);
                desired.Add(des);
            }

            return (current.ToArray(), desired.ToArray());
        }

        static (TestRuntimeModule, TestModule) GetTestModulePair()
        {
            string name = $"module_{Guid.NewGuid()}";
            IDictionary<string, EnvVal> envVars = new Dictionary<string, EnvVal>();

            return (
                new TestRuntimeModule(
                        name,
                        "version1",
                        RestartPolicy.Always,
                        "test",
                        ModuleStatus.Running,
                        Config,
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Running,
                        ImagePullPolicy.OnCreate,
                        null,
                        envVars),
                new TestModule(
                        name,
                        "version1",
                        "test",
                        ModuleStatus.Running,
                        Config,
                        RestartPolicy.Always,
                        ImagePullPolicy.OnCreate,
                        DefaultConfigurationInfo,
                        envVars)
             );
        }
    }
}
