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
                "OperatingSystemType=;Architecture=;Version=;ServerVersion=;KernelVersion=;OperatingSystem=;NumCpus=0;Virtualized=;",
                new SystemInfo(null, null, null, null, null, null, null, 0, null, null).ToQueryString());
        }

        [Fact]
        [Unit]
        public void BasicValueTest()
        {
            Assert.Equal(
                "OperatingSystemType=A;Architecture=B;Version=C;ServerVersion=D;KernelVersion=E;OperatingSystem=F;NumCpus=0;Virtualized=G;",
                new SystemInfo("A", "B", "C", ProvisioningInfo.Empty, "D", "E", "F", 0, "G", new Dictionary<string, object>()).ToQueryString());
        }

        [Fact]
        [Unit]
        public void EncodedValueTest()
        {
            Assert.Equal(
                "OperatingSystemType=A+A;Architecture=B%2BB;Version=C+C;ServerVersion=D%2BD;KernelVersion=E+E;OperatingSystem=F%2BF;NumCpus=0;Virtualized=G+G;",
                new SystemInfo("A A", "B+B", "C C", ProvisioningInfo.Empty, "D+D", "E E", "F+F", 0, "G G", new Dictionary<string, object>()).ToQueryString());
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
                "OperatingSystemType=A;Architecture=B;Version=C;ServerVersion=;KernelVersion=;OperatingSystem=;NumCpus=0;Virtualized=;"
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
                "OperatingSystemType=A;Architecture=B;Version=C;ServerVersion=;KernelVersion=;OperatingSystem=;NumCpus=0;Virtualized=;"
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
                "OperatingSystemType=A;Architecture=B;Version=C;ServerVersion=;KernelVersion=;OperatingSystem=;NumCpus=0;Virtualized=;"
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
                "OperatingSystemType=A;Architecture=B;Version=C;ServerVersion=;KernelVersion=;OperatingSystem=;NumCpus=0;Virtualized=;"
                + "first+first=1%2B1;second%2Bsecond=2+2;third+third=3%2B3;",
                new SystemInfo("A", "B", "C", new ReadOnlyDictionary<string, object>(properties)).ToQueryString());
        }

        [Fact]
        [Unit]
        public void FlattenedSerializationTest()
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();

            properties.Add("first", "1");
            properties.Add("second", "2");
            properties.Add("third", "3");

            SystemInfo systemInfo = new SystemInfo("A", "B", "C", new ReadOnlyDictionary<string, object>(properties));
            Assert.Equal(
                @"{""OperatingSystemType"":""A"",""Architecture"":""B"",""Version"":""C"",""Provisioning"":{""Type"":"""",""DynamicReprovisioning"":false},""ServerVersion"":"""",""KernelVersion"":"""",""OperatingSystem"":"""",""NumCpus"":0,""Virtualized"":"""",""first"":""1"",""second"":""2"",""third"":""3""}",
                Newtonsoft.Json.JsonConvert.SerializeObject(systemInfo));
        }
    }
}
