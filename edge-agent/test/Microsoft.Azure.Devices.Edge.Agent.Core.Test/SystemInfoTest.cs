// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class SystemInfoTest
    {
        [Fact]
        [Unit]
        public void NullEntryTest()
        {
            Assert.Equal(
                "kernel_name=;kernel_release=;kernel_version=;"
                + "os_name=;os_version=;os_variant=;os_build_id=;"
                + "cpu_architecture=;"
                + "product_name=;product_vendor=;",
                new SystemInfo(null, null, null, null, null, null, null, null, 0, null, null, null, null, null, null).ToQueryString());
        }

        [Fact]
        [Unit]
        public void BasicValueTest()
        {
            Assert.Equal(
                "kernel_name=A;kernel_release=B;kernel_version=C;"
                + "os_name=D;os_version=E;os_variant=F;os_build_id=G;"
                + "cpu_architecture=H;"
                + "product_name=J;product_vendor=K;",
                new SystemInfo("A", "B", "C", "D", "E", "F", "G", "H", 0, "I", "J", "K", "L", null, null).ToQueryString());
        }

        [Fact]
        [Unit]
        public void EncodedValueTest()
        {
            Assert.Equal(
                "kernel_name=A+A;kernel_release=B%2BB;kernel_version=C+C;"
                + "os_name=D%2BD;os_version=E+E;os_variant=F%2BF;os_build_id=G+G;"
                + "cpu_architecture=H%2BH;"
                + "product_name=J%2BJ;product_vendor=K+K;",
                new SystemInfo("A A", "B+B", "C C", "D+D", "E E", "F+F", "G G", "H+H", 0, "I I", "J+J", "K K", "L+L", null, null).ToQueryString());
        }

        [Fact]
        [Unit]
        public void BasicEntryTest()
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            properties.Add("first", "1");
            properties.Add("second", "2");
            properties.Add("third", "3");

            Assert.Equal(
                "kernel_name=A;kernel_release=B;kernel_version=C;"
                + "os_name=D;os_version=E;os_variant=F;os_build_id=G;"
                + "cpu_architecture=H;"
                + "product_name=J;product_vendor=K;"
                + "first=1;second=2;third=3;",
                new SystemInfo("A", "B", "C", "D", "E", "F", "G", "H", 0, "I", "J", "K", "L", null, properties).ToQueryString());
        }

        [Fact]
        [Unit]
        public void NullOrEmptyKeyTest()
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            properties.Add("first", "1");
            properties.Add(string.Empty, "foo");
            properties.Add("second", "2");
            properties.Add("third", "3");

            Assert.Equal(
                "kernel_name=A;kernel_release=B;kernel_version=C;"
                + "os_name=D;os_version=E;os_variant=F;os_build_id=G;"
                + "cpu_architecture=H;"
                + "product_name=J;product_vendor=K;"
                + "first=1;second=2;third=3;",
                new SystemInfo("A", "B", "C", "D", "E", "F", "G", "H", 0, "I", "J", "K", "L", null, properties).ToQueryString());
        }

        [Fact]
        [Unit]
        public void NullValueTest()
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            properties.Add("first", "1");
            properties.Add("second", null);
            properties.Add("third", "3");

            Assert.Equal(
                "kernel_name=A;kernel_release=B;kernel_version=C;"
                + "os_name=D;os_version=E;os_variant=F;os_build_id=G;"
                + "cpu_architecture=H;"
                + "product_name=J;product_vendor=K;"
                + "first=1;second=;third=3;",
                new SystemInfo("A", "B", "C", "D", "E", "F", "G", "H", 0, "I", "J", "K", "L", null, properties).ToQueryString());
        }

        [Fact]
        [Unit]
        public void EncodedEntryTest()
        {
            IDictionary<string, string> properties = new Dictionary<string, string>();

            properties.Add("first first", "1+1");
            properties.Add("second+second", "2 2");
            properties.Add("third third", "3+3");

            Assert.Equal(
                "kernel_name=A;kernel_release=B;kernel_version=C;"
                + "os_name=D;os_version=E;os_variant=F;os_build_id=G;"
                + "cpu_architecture=H;"
                + "product_name=J;product_vendor=K;"
                + "first+first=1%2B1;second%2Bsecond=2+2;third+third=3%2B3;",
                new SystemInfo("A", "B", "C", "D", "E", "F", "G", "H", 0, "I", "J", "K", "L", null, properties).ToQueryString());
        }
    }
}
