// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using System.Collections.Generic;
    using System.Text;
    using Xunit;

    [Unit]
    public class KubeUtilsTest
    {

        [Theory]
        [InlineData("edgeAgent", "$edgeAgent")]
        [InlineData("edgeHub", "$edgeHub")]
        [InlineData("iothub.azure-device.net/edgeAgent", "iothub.azure-device.net/$edgeAgent")]
        [InlineData("iothub.azure-device.net/edgeHub", "iothub.azure-device.net/$edgeHub")]
        [InlineData("k8s.io/a-0", "K8S.IO/---a-0--")]
        public void SanitizeAnnotationKeyTest(string expected, string raw) => Assert.Equal(expected, KubeUtils.SanitizeAnnotationKey(raw));

        [Theory]
        [InlineData("edgeagent", "$edgeAgent")]
        [InlineData("edgehub", "$edgeHub")]
        [InlineData("---a-0---", "---a-0---")]
        [MemberData(nameof(GetLongDnsDomain))]
        public void SanitizeK8sValueTest(string expected, string raw) => Assert.Equal(expected, KubeUtils.SanitizeK8sValue(raw));

        [Theory]
        [InlineData("edgeagent", "$edgeAgent")]
        [InlineData("edgehub", "$edgeHub")]
        // all characters are forced lowercase.
        // length is <= 63 characters
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJ")]
        // Allow '-'
        [InlineData("abcdefghi-abcdefghi-abcdefghi-abcdefghi-abcdefghi-abcdefghi-abc", "ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHI-ABCDEFGHIJ")]
        // Must start with alphabet and end with alphanumeric
        [InlineData("a-0", "---a-0---")]
        [InlineData("z-9", "---z-9---")]
        [InlineData("a-0", "---A-0---")]
        [InlineData("z-9", "---Z-9---")]
        [InlineData("a-z", "---a-z---")]
        [InlineData("a-z---1", "---a-z-/--1")]
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabj", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB-------J")]
        public void SanitizeDnsValueTest(string expected, string raw) => Assert.Equal(expected, KubeUtils.SanitizeDNSValue(raw));

        public static IEnumerable<object[]> GetLongDnsDomain()
        {
            var raw_output = new StringBuilder();
            var cooked_output = new StringBuilder();
            for (int i = 0; i <= 255; i+=2)
            {
                raw_output.Append((char)('a' + (i % 26)));
                raw_output.Append('.');
                if (i <= 253)
                {
                    cooked_output.Append((char)('a' + (i % 26)));
                }
                if (i < 252)
                {
                    cooked_output.Append('.');
                }

            }

            yield return new object[] { cooked_output.ToString(), raw_output.ToString() };
        }

        [Theory]
        // must be a one or more DNS labels separated by dots (.), not longer than 253 characters in total
        [InlineData("edgeagent", "$edgeAgent")]
        [InlineData("edgehub", "$edgeHub")]
        [InlineData("a-0.org", "---a-0---.org")]
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabj.com", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB-------J.com")]
        [MemberData(nameof(GetLongDnsDomain))]
        public void SanitizeDNSDomainTest(string expected, string raw) => Assert.Equal(expected, KubeUtils.SanitizeDNSDomain(raw));

        [Theory]
        [InlineData("edgeagent", "$edgeAgent")]
        [InlineData("edgehub", "$edgeHub")]
        // length is <= 63 characters, lowercase
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJ")]
        // must be all alphanumeric characters or ['-','.','_']
        [InlineData("a-b_c.d", "a$?/-b#@_c=+.d")]
        // must start with an alphabet
        // must end with an alphanumeric character
        [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabj", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB-------J")]
        [InlineData("zz", "$-._/zz$-._/")]
        [InlineData("z9", "$-._/z9$-._/")]
        public void SanitizeLabelValueTest(string expected, string raw) => Assert.Equal(expected, KubeUtils.SanitizeLabelValue(raw));
    }
}
