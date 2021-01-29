// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class KubeUtilsTest
    {
        public static IEnumerable<object[]> GetLongDnsDomain()
        {
            var raw_output = new StringBuilder();
            for (int i = 0; i <= 255; i += 2)
            {
                raw_output.Append((char)('a' + (i % 26)));
                raw_output.Append('.');
            }

            yield return new object[] { raw_output.ToString() };
        }

        [Theory]
        [InlineData("edgeAgent", "$edgeAgent")]
        [InlineData("edgeHub", "$edgeHub")]
        [InlineData("iothub.azure-device.net/edgeAgent", "iothub.azure-device.net/$edgeAgent")]
        [InlineData("iothub.azure-device.net/edgeHub", "iothub.azure-device.net/$edgeHub")]
        [InlineData("k8s.io/a-0", "K8S.IO/---a-0--")]
        public void SanitizeAnnotationKeyTest(string expected, string raw) => Assert.Equal(expected, KubeUtils.SanitizeAnnotationKey(raw));

        [Theory]
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghij")]
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijab-------j.com/a")]
        [InlineData("iothub.azure-device.net/abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghij")]
        [InlineData(null)]
        [InlineData("!!!")]
        [InlineData("!!!!/abc")]
        [InlineData("123.com/!!!!")]
        public void SanitizeAnnotationKeyFailTest(string raw) => Assert.Throws<InvalidKubernetesNameException>(() => KubeUtils.SanitizeAnnotationKey(raw));

        [Theory]
        [InlineData("edgeagent", "$edgeAgent")]
        [InlineData("edgehub", "$edgeHub")]
        [InlineData("---a-0---", "---a-0---")]
        [InlineData("000", "000")]
        [InlineData(null, null)]
        public void SanitizeK8sValueTest(string expected, string raw) => Assert.Equal(expected, KubeUtils.SanitizeK8sValue(raw));

        [Theory]
        [MemberData(nameof(GetLongDnsDomain))]
        public void SanitizeK8sValueFailTest(string raw) => Assert.Throws<InvalidKubernetesNameException>(() => KubeUtils.SanitizeK8sValue(raw));

        [Theory]
        [InlineData("edgeagent", "$edgeAgent")]
        [InlineData("edgehub", "$edgeHub")]
        // all characters are forced lowercase.
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABC")]
        // Allow '-'
        [InlineData("abcdefghi-abcdefghi-abcdefghi-abcdefghi-abcdefghi-abcdefghi-abc", "ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABC")]
        // Must start with alphabet and end with alphanumeric
        [InlineData("a-0", "---a-0---")]
        [InlineData("z-9", "---z-9---")]
        [InlineData("a-0", "---A-0---")]
        [InlineData("z-9", "---Z-9---")]
        [InlineData("a-z", "---a-z---")]
        [InlineData("a-z---1", "---a-z-/--1")]
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijab----------c", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB----------C")]
        public void SanitizeDnsValueTest(string expected, string raw) => Assert.Equal(expected, KubeUtils.SanitizeDNSValue(raw));

        [Theory]
        // length is <= 63 characters
        [InlineData("ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJ")]
        [InlineData("ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHIJ")]
        // Must start with alphabet and end with alphanumeric
        [InlineData("ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB-------J")]
        [InlineData(null)]
        [InlineData("$$$$$$")]
        public void SanitizeDnsValueFailTest(string raw) => Assert.Throws<InvalidKubernetesNameException>(() => KubeUtils.SanitizeDNSValue(raw));

        [Theory]
        [InlineData("edgeagent", "$edgeAgent")]
        [InlineData("edgehub", "$edgeHub")]
        // all characters are forced lowercase.
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABC")]
        // Allow '-'
        [InlineData("abcdefghi-abcdefghi-abcdefghi-abcdefghi-abcdefghi-abcdefghi-abc", "ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABC")]
        // Must start with alphabet and end with alphanumeric
        [InlineData("a-0", "---a-0---")]
        [InlineData("z-9", "---z-9---")]
        [InlineData("a-0", "---A-0---")]
        [InlineData("z-9", "---Z-9---")]
        [InlineData("a-z", "---a-z---")]
        [InlineData("a-z---1", "---a-z-/--1")]
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijab----------c", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB----------C")]
        [InlineData("111111111", "111111111")]
        public void SanitizeDnsLabelTest(string expected, string raw) => Assert.Equal(expected, KubeUtils.SanitizeDNSLabel(raw));

        [Theory]
        // length is <= 63 characters
        [InlineData("ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJ")]
        [InlineData("ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHIJ")]
        // Must start with alphabet and end with alphanumeric
        [InlineData("ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB-------J")]
        [InlineData(null)]
        [InlineData("$$$$$$")]
        public void SanitizeDnsLabelFailTest(string raw) => Assert.Throws<InvalidKubernetesNameException>(() => KubeUtils.SanitizeDNSLabel(raw));

        [Theory]
        // must be a one or more DNS labels separated by dots (.), not longer than 253 characters in total
        [InlineData("edgeagent", "$edgeAgent")]
        [InlineData("edgehub", "$edgeHub")]
        [InlineData("a-0.org", "---a-0---.org")]
        [InlineData("a-0---b.org", "---a-0---b.org")]
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijab----------c.com", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB----------C.com")]
        public void SanitizeDNSDomainTest(string expected, string raw) => Assert.Equal(expected, KubeUtils.SanitizeDNSDomain(raw));

        [Theory]
        // must be a one or more DNS labels (< 63 chars) separated by dots (.), not longer than 253 characters in total
        [InlineData("ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB-------J.com")]
        [MemberData(nameof(GetLongDnsDomain))]
        [InlineData(null)]
        [InlineData("     ")]
        [InlineData("$$$$.com")]
        [InlineData("a.&&&&.org")]
        public void SanitizeDNSDomainFailTest(string raw) => Assert.Throws<InvalidKubernetesNameException>(() => KubeUtils.SanitizeDNSDomain(raw));

        [Theory]
        [InlineData("edgeagent", "$edgeAgent")]
        [InlineData("edgehub", "$edgeHub")]
        [InlineData("12device", "12device")]
        [InlineData("345hub-name.org", "345hub-name.org")]
        // length is <= 63 characters, lowercase
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABC")]
        // must be all alphanumeric characters or ['-','.','_']
        [InlineData("a-b_c.d", "a$?/-b#@_c=+.d")]
        // must start with an alphabet
        // must end with an alphanumeric character
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijab----------c", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB----------C")]
        [InlineData("zz", "$-._/zz$-._/")]
        [InlineData("z9", "$-._/z9$-._/")]
        public void SanitizeLabelValueTest(string expected, string raw) => Assert.Equal(expected, KubeUtils.SanitizeLabelValue(raw));

        [Theory]
        // length is <= 63 characters, lowercase
        [InlineData("ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCD")]
        // must start with an alphabet
        // must end with an alphanumeric character
        [InlineData("ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB-------J")]
        [InlineData(null)]
        [InlineData("    ")]
        [InlineData("$%^&")]
        public void SanitizeLabelValueFailTest(string raw) => Assert.Throws<InvalidKubernetesNameException>(() => KubeUtils.SanitizeLabelValue(raw));
    }
}
