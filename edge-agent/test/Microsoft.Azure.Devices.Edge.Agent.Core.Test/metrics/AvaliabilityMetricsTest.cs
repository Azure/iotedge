// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Akka.Event;
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
    public class AvaliabilityMetricsTest : TempDirectory
    {
        static readonly TestConfig Config = new TestConfig("image1");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();

        [Fact]
        public void ReportsAccurateAvaliability()
        {
            /* Setup */
            Dictionary<string, double> uptimes = new Dictionary<string, double>();
            Action<double, string[]> onSet = (val, list) =>
            {
                uptimes[list[0]] = val;
            };
            var guage = new Mock<IMetricsGauge>();
            guage.Setup(x => x.Set(It.IsAny<double>(), It.IsAny<string[]>())).Callback(onSet);

            var metricsProvider = new Mock<IMetricsProvider>();
            metricsProvider.Setup(x => x.CreateGauge(
                    "lifetime_avaliability",
                    "total availability since deployment",
                    new List<string> { "module_name", "module_version" }))
                .Returns(guage.Object);

            metricsProvider.Setup(x => x.CreateGauge(
                    "weekly_avaliability",
                    "total availability for the last 7 days",
                    new List<string> { "module_name", "module_version" }))
                .Returns(new Mock<IMetricsGauge>().Object);

            Util.Metrics.Metrics.Init(metricsProvider.Object, new NullMetricsListener(), NullLogger.Instance);

            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = DateTime.Now;
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            AvaliabilityMetrics.Time = systemTime.Object;

            (TestRuntimeModule[] current, TestModule[] desired) = GetTestModules(3);
            ModuleSet currentModuleSet = ModuleSet.Create(current as IModule[]);
            ModuleSet desiredModuleSet = ModuleSet.Create(desired);

            /* Test */
            AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);
            Assert.Empty(uptimes);

            fakeTime = fakeTime.AddMinutes(10);
            AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);
            Assert.Equal(3, uptimes.Count);
            Assert.Equal(1, uptimes[current[0].Name]);
            Assert.Equal(1, uptimes[current[1].Name]);
            Assert.Equal(1, uptimes[current[2].Name]);

            fakeTime = fakeTime.AddMinutes(10);
            current[1].RuntimeStatus = ModuleStatus.Failed;
            current[2].RuntimeStatus = ModuleStatus.Failed;
            AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);
            Assert.Equal(3, uptimes.Count);
            Assert.Equal(1, uptimes[current[0].Name]);
            Assert.Equal(.5, uptimes[current[1].Name]);
            Assert.Equal(.5, uptimes[current[2].Name]);

            fakeTime = fakeTime.AddMinutes(20);
            current[1].RuntimeStatus = ModuleStatus.Running;
            AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);
            Assert.Equal(3, uptimes.Count);
            Assert.Equal(1, uptimes[current[0].Name]);
            Assert.Equal(.75, uptimes[current[1].Name]);
            Assert.Equal(.25, uptimes[current[2].Name]);
        }

        [Fact]
        public void PersistsUptime()
        {
            /* Setup */
            Dictionary<string, double> uptimes = new Dictionary<string, double>();
            Action<double, string[]> onSet = (val, list) =>
            {
                uptimes[list[0]] = val;
            };
            var guage = new Mock<IMetricsGauge>();
            guage.Setup(x => x.Set(It.IsAny<double>(), It.IsAny<string[]>())).Callback(onSet);

            var metricsProvider = new Mock<IMetricsProvider>();
            metricsProvider.Setup(x => x.CreateGauge(
                    "lifetime_avaliability",
                    "total availability since deployment",
                    new List<string> { "module_name", "module_version" }))
                .Returns(guage.Object);

            metricsProvider.Setup(x => x.CreateGauge(
                    "weekly_avaliability",
                    "total availability for the last 7 days",
                    new List<string> { "module_name", "module_version" }))
                .Returns(new Mock<IMetricsGauge>().Object);

            Util.Metrics.Metrics.Init(metricsProvider.Object, new NullMetricsListener(), NullLogger.Instance);

            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = DateTime.Now;
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            string directory = this.GetTempDir();

            (TestRuntimeModule[] current, TestModule[] desired) = GetTestModules(3);
            ModuleSet currentModuleSet = ModuleSet.Create(current as IModule[]);
            ModuleSet desiredModuleSet = ModuleSet.Create(desired);

            /* make fake restart func */
            ConstructorInfo constructor = typeof(AvaliabilityMetrics).GetConstructor(BindingFlags.Static | BindingFlags.NonPublic, null, new Type[0], null);
            constructor.Invoke(null, null);
            MethodInfo saveData = typeof(AvaliabilityMetrics).GetMethod("SaveData", BindingFlags.NonPublic | BindingFlags.Static);
            object[] parameters = { null, null };
            Action fakeRestart = () =>
            {
                /* save data */
                saveData.Invoke(null, parameters);

                /* reset metrics */
                constructor.Invoke(null, null);
                AvaliabilityMetrics.Time = systemTime.Object;
                AvaliabilityMetrics.StoragePath = Option.Some(directory);

                /* simulate long time shutdown */
                fakeTime = fakeTime.AddMinutes(1000);
                AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);
            };

            AvaliabilityMetrics.Time = systemTime.Object;
            AvaliabilityMetrics.StoragePath = Option.Some(directory);

            /* Test */
            Assert.Empty(uptimes);
            AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);
            fakeTime = fakeTime.AddMinutes(10);
            AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);
            Assert.Equal(3, uptimes.Count);
            Assert.Equal(1, uptimes[current[0].Name]);
            Assert.Equal(1, uptimes[current[1].Name]);
            Assert.Equal(1, uptimes[current[2].Name]);
            fakeRestart();

            fakeTime = fakeTime.AddMinutes(10);
            current[1].RuntimeStatus = ModuleStatus.Failed;
            current[2].RuntimeStatus = ModuleStatus.Failed;
            AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);
            Assert.Equal(3, uptimes.Count);
            Assert.Equal(1, uptimes[current[0].Name]);
            Assert.Equal(.5, uptimes[current[1].Name]);
            Assert.Equal(.5, uptimes[current[2].Name]);
            fakeRestart();

            fakeTime = fakeTime.AddMinutes(20);
            current[1].RuntimeStatus = ModuleStatus.Running;
            AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);
            Assert.Equal(3, uptimes.Count);
            Assert.Equal(1, uptimes[current[0].Name]);
            Assert.Equal(.75, uptimes[current[1].Name]);
            Assert.Equal(.25, uptimes[current[2].Name]);
            fakeRestart();

            Assert.Equal(3, uptimes.Count);
            Assert.Equal(1, uptimes[current[0].Name]);
            Assert.Equal(.75, uptimes[current[1].Name]);
            Assert.Equal(.25, uptimes[current[2].Name]);
        }

        private static (TestRuntimeModule[], TestModule[]) GetTestModules(int num)
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

        private static (TestRuntimeModule, TestModule) GetTestModulePair()
        {
            string name = $"module_{Guid.NewGuid()}";
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
                        EnvVars),
                new TestModule(
                        name,
                        "version1",
                        "test",
                        ModuleStatus.Running,
                        Config,
                        RestartPolicy.Always,
                        ImagePullPolicy.OnCreate,
                        DefaultConfigurationInfo,
                        EnvVars)
             );
        }
    }
}
