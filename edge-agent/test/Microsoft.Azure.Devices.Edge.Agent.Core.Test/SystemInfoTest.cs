// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class SystemInfoTest
    {
        [Fact]
        [Unit]
        public void NullEntryTest()
        {
            Assert.Equal(
                "kernel=;architecture=;version=;server_version=;kernel_version=;operating_system=;cpus=0;total_memory=0;virtualized=;",
                new SystemInfo(null, null, null, null, null, null, null, 0, 0, null, null).ToQueryString());
        }

        [Fact]
        [Unit]
        public void BasicValueTest()
        {
            Assert.Equal(
                "kernel=A;architecture=B;version=C;server_version=D;kernel_version=E;operating_system=F;cpus=1;total_memory=2;virtualized=G;",
                new SystemInfo("A", "B", "C", ProvisioningInfo.Empty, "D", "E", "F", 1, 2, "G", new Dictionary<string, object>()).ToQueryString());
        }

        [Fact]
        [Unit]
        public void EncodedValueTest()
        {
            Assert.Equal(
                "kernel=A+A;architecture=B%2BB;version=C+C;server_version=D%2BD;kernel_version=E+E;operating_system=F%2BF;cpus=1;total_memory=2;virtualized=G+G;",
                new SystemInfo("A A", "B+B", "C C", ProvisioningInfo.Empty, "D+D", "E E", "F+F", 1, 2, "G G", new Dictionary<string, object>()).ToQueryString());
        }

        [Fact]
        [Unit]
        public void BasicEntryTest()
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();

            properties.Add("first", "1");
            properties.Add("second", "2");
            properties.Add("third", "3");

            Assert.Equal(
                "kernel=A;architecture=B;version=C;server_version=;kernel_version=;operating_system=;cpus=0;total_memory=0;virtualized=;"
                + "first=1;second=2;third=3;",
                new SystemInfo("A", "B", "C", new ReadOnlyDictionary<string, object>(properties)).ToQueryString());
        }

        [Fact]
        [Unit]
        public void NullOrEmptyKeyTest()
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();

            properties.Add("first", "1");
            properties.Add(string.Empty, "foo");
            properties.Add("second", "2");
            properties.Add("third", "3");

            Assert.Equal(
                "kernel=A;architecture=B;version=C;server_version=;kernel_version=;operating_system=;cpus=0;total_memory=0;virtualized=;"
                + "first=1;second=2;third=3;",
                new SystemInfo("A", "B", "C", new ReadOnlyDictionary<string, object>(properties)).ToQueryString());
        }

        [Fact]
        [Unit]
        public void NullValueTest()
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();

            properties.Add("first", "1");
            properties.Add("second", null);
            properties.Add("third", "3");

            Assert.Equal(
                "kernel=A;architecture=B;version=C;server_version=;kernel_version=;operating_system=;cpus=0;total_memory=0;virtualized=;"
                + "first=1;second=;third=3;",
                new SystemInfo("A", "B", "C", new ReadOnlyDictionary<string, object>(properties)).ToQueryString());
        }

        [Fact]
        [Unit]
        public void EncodedEntryTest()
        {
            IDictionary<string, object> properties = new Dictionary<string, object>();

            properties.Add("first first", "1+1");
            properties.Add("second+second", "2 2");
            properties.Add("third third", "3+3");

            Assert.Equal(
                "kernel=A;architecture=B;version=C;server_version=;kernel_version=;operating_system=;cpus=0;total_memory=0;virtualized=;"
                + "first+first=1%2B1;second%2Bsecond=2+2;third+third=3%2B3;",
                new SystemInfo("A", "B", "C", new ReadOnlyDictionary<string, object>(properties)).ToQueryString());
        }

        [Fact]
        [Unit]
        public void MetricsSerializationTest()
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();

            properties.Add("first", "1");
            properties.Add("second", "2");
            properties.Add("third", "3");

            SystemInfo systemInfo = new SystemInfo("A", "B", "C", new ReadOnlyDictionary<string, object>(properties));
            Assert.Equal(
                @"{""OperatingSystemType"":""A"",""Architecture"":""B"",""Version"":""C"",""Provisioning"":{""Type"":"""",""DynamicReprovisioning"":false},""ServerVersion"":"""",""KernelVersion"":"""",""OperatingSystem"":"""",""NumCpus"":0,""TotalMemory"":0,""Virtualized"":""""}",
                Newtonsoft.Json.JsonConvert.SerializeObject(systemInfo));
        }
    }
}
