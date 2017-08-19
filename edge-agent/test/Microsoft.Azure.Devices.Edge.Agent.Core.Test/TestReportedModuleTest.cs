// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;
    using Newtonsoft.Json.Linq;
    using System.Linq;
    using System.Collections.Generic;


    [ExcludeFromCodeCoverage]
    public class TestReportedModuleTest
    {
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly TestConfig Config2 = new TestConfig("image2");
        static readonly TestConfig Config3 = new TestConfig("image1");

        public static TestModule TestModule1 = new TestModule("name", "version", "type", ModuleStatus.Running, Config1);

        public static TestReportedModule ReportedModule1 = new TestReportedModule("name", "version", "type", ModuleStatus.Running, Config1, 0, "statusDescription", "lastStartTime", "lastExitTime");
        public static TestReportedModule ReportedModule2 = new TestReportedModule("name", "version", "type", ModuleStatus.Running, Config2, 0, "statusDescription", "lastStartTime", "lastExitTime");
        public static TestReportedModule ReportedModule3 = new TestReportedModule("name", "version", "type", ModuleStatus.Running, Config3, 0, "statusDescription", "lastStartTime", "lastExitTime");

        public static TestReportedModule ReportedModule4 = new TestReportedModule("name", "version", "type", ModuleStatus.Running, Config3, -1, "statusDescription", "lastStartTime", "lastExitTime");
        public static TestReportedModule ReportedModule5 = new TestReportedModule("name", "version", "type", ModuleStatus.Running, Config3, 0, "status is different", "lastStartTime", "lastExitTime");
        public static TestReportedModule ReportedModule6 = new TestReportedModule("name", "version", "type", ModuleStatus.Running, Config3, 0, "statusDescription", "last start time is different", "lastExitTime");
        public static TestReportedModule ReportedModule7 = new TestReportedModule("name", "version", "type", ModuleStatus.Running, Config3, 0, "statusDescription", "lastStartTime", "last exit is different");

        [Fact]
        [Unit]
        public static void TestEquality()
        {
            var reportedModuleReference = ReportedModule1;

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
        public static void TestNullIsOK()
        {
            var reportedModule = new TestReportedModule(TestModule1.Name, TestModule1.Version, TestModule1.Type, TestModule1.Status, TestModule1.Config, 0, null, null, null);

            Assert.True(TestModule1.Equals(reportedModule));
            Assert.True(reportedModule.Equals(TestModule1));
            Assert.False(reportedModule.Equals(ReportedModule1));
        }
    }
}