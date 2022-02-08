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
                "kernel_name=;cpu_architecture=;",
                new SystemInfo(null, null, null, null).ToQueryString());
        }

        [Fact]
        [Unit]
        public void BasicValueTest()
        {
            Assert.Equal(
                "kernel_name=A;cpu_architecture=B;",
                new SystemInfo("A", "B", "C").ToQueryString());
        }

        [Fact]
        [Unit]
        public void EncodedValueTest()
        {
            Assert.Equal(
                "kernel_name=A+A;cpu_architecture=B%2BB;",
                new SystemInfo("A A", "B+B", "C C").ToQueryString());
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
                "kernel_name=A;cpu_architecture=B;"
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
                "kernel_name=A;cpu_architecture=B;"
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
                "kernel_name=A;cpu_architecture=B;"
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
                "kernel_name=A;cpu_architecture=B;"
                + "first+first=1%2B1;second%2Bsecond=2+2;third+third=3%2B3;",
                new SystemInfo("A", "B", "C", new ReadOnlyDictionary<string, object>(properties)).ToQueryString());
        }
    }
}
