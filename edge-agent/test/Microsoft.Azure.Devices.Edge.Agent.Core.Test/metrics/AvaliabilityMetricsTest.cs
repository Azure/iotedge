using Akka.Event;
using App.Metrics;
using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Azure.Devices.Edge.Util.Metrics;
using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;
using Microsoft.Azure.Devices.Edge.Util.Test.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Metrics
{
    [Unit]
    public class AvaliabilityMetricsTest : IDisposable
    {
        static readonly TestConfig Config = new TestConfig("image1");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        private string tempDir;

        public AvaliabilityMetricsTest()
        {
            tempDir = Path.Combine(Path.GetTempPath(), $"av_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
        }

        public void Dispose()
        {
           Directory.Delete(tempDir, true);
        }

        [Fact]
        public void ReportsAccurateAvaliability()
        {
            Dictionary<string, double> uptimes = new Dictionary<string, double>();
            Action<double, string[]> onSet = (val, list) =>
            {
                uptimes[list[0]] = val;
            };
            var guage = new Mock<IMetricsGauge>();
            guage.Setup(x => x.Set(It.IsAny<double>(), It.IsAny<string[]>())).Callback(onSet);

            var metricsProvider = new Mock<IMetricsProvider>();
            metricsProvider.Setup(x => x.CreateGauge("lifetime_avaliability",
                    "total availability since deployment",
                    new List<string> { "module_name", "module_version" }))
                .Returns(guage.Object);

            Util.Metrics.Metrics.Init(metricsProvider.Object, new NullMetricsListener(), NullLogger.Instance);

            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = DateTime.Now;
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            AvaliabilityMetrics.time = systemTime.Object;
            AvaliabilityMetrics.storagePath = Option.Some(tempDir);

            (TestRuntimeModule[] current, TestModule[] desired) = GetTestModules(3);
            ModuleSet currentModuleSet = ModuleSet.Create(current as IModule[]);
            ModuleSet desiredModuleSet = ModuleSet.Create(desired);

            AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);

            fakeTime = fakeTime.AddMinutes(10);
            AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);
            TestHelper.ApproxEqual(1, uptimes[current[0].Name], .05);
            TestHelper.ApproxEqual(1, uptimes[current[1].Name], .05);
            TestHelper.ApproxEqual(1, uptimes[current[2].Name], .05);

            fakeTime = fakeTime.AddMinutes(10);
            current[1].RuntimeStatus = ModuleStatus.Failed;
            current[2].RuntimeStatus = ModuleStatus.Failed;
            AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);
            TestHelper.ApproxEqual(1, uptimes[current[0].Name], .05);
            TestHelper.ApproxEqual(.5, uptimes[current[1].Name], .05);
            TestHelper.ApproxEqual(.5, uptimes[current[2].Name], .05);

            fakeTime = fakeTime.AddMinutes(20);
            current[1].RuntimeStatus = ModuleStatus.Running;
            AvaliabilityMetrics.ComputeAvaliability(desiredModuleSet, currentModuleSet);
            TestHelper.ApproxEqual(1, uptimes[current[0].Name], .05);
            TestHelper.ApproxEqual(.75, uptimes[current[1].Name], .05);
            TestHelper.ApproxEqual(.25, uptimes[current[2].Name], .05);
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
