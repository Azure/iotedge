// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class TestRuntimeModuleTest
    {
        static readonly TestConfig Config1 = new TestConfig("image1");
        public static TestModule TestModule1 = new TestModule("name", "version", "type", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), null);

        static readonly DateTime lastStartTime = DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind);
        static readonly DateTime lastExitTime = lastStartTime.AddDays(1);
        public static TestRuntimeModule ReportedModule1 = new TestRuntimeModule("name", "version", RestartPolicy.OnUnhealthy, "type", ModuleStatus.Running, Config1, 0, "statusDescription", lastStartTime, lastExitTime, 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly TestConfig Config2 = new TestConfig("image2");
        public static TestRuntimeModule ReportedModule2 = new TestRuntimeModule("name", "version", RestartPolicy.OnUnhealthy, "type", ModuleStatus.Running, Config2, 0, "statusDescription", lastStartTime, lastExitTime, 0, DateTime.MinValue, ModuleStatus.Running);
        static readonly TestConfig Config3 = new TestConfig("image1");
        public static TestRuntimeModule ReportedModule3 = new TestRuntimeModule("name", "version", RestartPolicy.OnUnhealthy, "type", ModuleStatus.Running, Config3, 0, "statusDescription", lastStartTime, lastExitTime, 0, DateTime.MinValue, ModuleStatus.Running);

        public static TestRuntimeModule ReportedModule4 = new TestRuntimeModule("name", "version", RestartPolicy.OnUnhealthy, "type", ModuleStatus.Running, Config3, -1, "statusDescription", lastStartTime, lastExitTime, 0, DateTime.MinValue, ModuleStatus.Running);
        public static TestRuntimeModule ReportedModule5 = new TestRuntimeModule("name", "version", RestartPolicy.OnUnhealthy, "type", ModuleStatus.Running, Config3, 0, "status is different", lastStartTime, lastExitTime, 0, DateTime.MinValue, ModuleStatus.Running);
        public static TestRuntimeModule ReportedModule6 = new TestRuntimeModule("name", "version", RestartPolicy.OnUnhealthy, "type", ModuleStatus.Running, Config3, 0, "statusDescription", lastStartTime.AddDays(-1), lastExitTime, 0, DateTime.MinValue, ModuleStatus.Running);
        public static TestRuntimeModule ReportedModule7 = new TestRuntimeModule("name", "version", RestartPolicy.OnUnhealthy, "type", ModuleStatus.Running, Config3, 0, "statusDescription", lastStartTime, lastExitTime.AddDays(1), 0, DateTime.MinValue, ModuleStatus.Running);

        [Fact]
        [Unit]
        public static void TestEquality()
        {
            TestRuntimeModule reportedModuleReference = ReportedModule1;

            Assert.False(ReportedModule1.Equals(null));
            Assert.True(ReportedModule1.Equals(reportedModuleReference));
            Assert.True(TestModule1.Equals(ReportedModule1));
            Assert.True(ReportedModule1.Equals(TestModule1));
            Assert.False(TestModule1.Equals(ReportedModule2));
            Assert.False(ReportedModule2.Equals(TestModule1));
            Assert.False(ReportedModule1.Equals(new object()));
            Assert.False(ReportedModule1.Equals(ReportedModule2));
            Assert.True(ReportedModule1.Equals(ReportedModule3));
            Assert.False(ReportedModule3.Equals(ReportedModule4));
            Assert.False(ReportedModule3.Equals(ReportedModule5));
            Assert.False(ReportedModule3.Equals(ReportedModule6));
            Assert.False(ReportedModule3.Equals(ReportedModule7));
        }

        [Fact]
        [Unit]
        public static void TestHashCode()
        {
            int hc1 = ReportedModule1.GetHashCode();
            int hc2 = ReportedModule2.GetHashCode();
            int hc3 = ReportedModule3.GetHashCode();
            int hc4 = ReportedModule4.GetHashCode();
            int hc5 = ReportedModule5.GetHashCode();
            int hc6 = ReportedModule6.GetHashCode();
            int hc7 = ReportedModule7.GetHashCode();
            Assert.True(hc1 == hc3);
            Assert.False(hc1 == hc2);
            Assert.False(hc3 == hc4);
            Assert.False(hc3 == hc5);
            Assert.False(hc3 == hc6);
            Assert.False(hc3 == hc7);
        }

        [Fact]
        [Unit]
        public static void TestNullIsOk()
        {
            var reportedModule = new TestRuntimeModule(TestModule1.Name, TestModule1.Version, TestModule1.RestartPolicy, TestModule1.Type, ModuleStatus.Running, TestModule1.Config, 0, null, DateTime.MinValue, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);

            Assert.True(TestModule1.Equals(reportedModule));
            Assert.True(reportedModule.Equals(TestModule1));
            Assert.False(reportedModule.Equals(ReportedModule1));
        }

        [Fact]
        [Unit]
        public static void TestWithRuntimeStatus()
        {
            var reportedModule = new TestRuntimeModule(TestModule1.Name, TestModule1.Version, TestModule1.RestartPolicy, TestModule1.Type, ModuleStatus.Running, TestModule1.Config, 0, null, DateTime.MinValue, DateTime.MinValue, 0, DateTime.MinValue, ModuleStatus.Running);
            var updatedModule = (TestRuntimeModule)reportedModule.WithRuntimeStatus(ModuleStatus.Unknown);

            Assert.True(reportedModule.RuntimeStatus != updatedModule.RuntimeStatus);
            Assert.True(updatedModule.RuntimeStatus == ModuleStatus.Unknown);
            Assert.Equal(reportedModule.Config, updatedModule.Config);
            Assert.Equal(reportedModule.ConfigurationInfo, updatedModule.ConfigurationInfo);
            Assert.Equal(reportedModule.DesiredStatus, updatedModule.DesiredStatus);
            Assert.Equal(reportedModule.ExitCode, updatedModule.ExitCode);
            Assert.Equal(reportedModule.LastExitTimeUtc, updatedModule.LastExitTimeUtc);
            Assert.Equal(reportedModule.LastRestartTimeUtc, updatedModule.LastRestartTimeUtc);
            Assert.Equal(reportedModule.LastStartTimeUtc, updatedModule.LastStartTimeUtc);
            Assert.Equal(reportedModule.Name, updatedModule.Name);
            Assert.Equal(reportedModule.RestartCount, updatedModule.RestartCount);
            Assert.Equal(reportedModule.RestartPolicy, updatedModule.RestartPolicy);
            Assert.Equal(reportedModule.ImagePullPolicy, updatedModule.ImagePullPolicy);
            Assert.Equal(reportedModule.StatusDescription, updatedModule.StatusDescription);
            Assert.Equal(reportedModule.Type, updatedModule.Type);
            Assert.Equal(reportedModule.Version, updatedModule.Version);
        }
    }
}
