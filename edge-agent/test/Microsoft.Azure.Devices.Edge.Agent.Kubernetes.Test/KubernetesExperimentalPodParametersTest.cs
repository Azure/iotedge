// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Unit]
    public class KubernetesExperimentalPodParametersTest
    {
        [Theory]
        [MemberData(nameof(EmptyOptions))]
        public void ReturnsNoneWhenParseMissedOptions(IDictionary<string, JToken> experimental)
        {
            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental);

            Assert.Equal(Option.None<KubernetesExperimentalCreatePodParameters>(), parameters);
        }

        public static IEnumerable<object[]> EmptyOptions =>
            new List<object[]>
            {
                new object[] { null },
                new object[] { new Dictionary<string, JToken>() }
            };

        [Fact]
        public void ReturnsEmptySectionsWhenNoSectionsProvided()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{}")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.False(parameters.NodeSelector.HasValue);
        }

        [Fact]
        public void ReturnsEmptySectionsWhenWrongOptionsProvided()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("\"\"")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental);

            Assert.False(parameters.HasValue);
        }

        [Fact]
        public void IgnoresUnsupportedOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ a: { a: \"b\" } }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            // Assert.False(parameters.Volumes.HasValue);
            Assert.False(parameters.NodeSelector.HasValue);
            // Assert.False(parameters.Resources.HasValue);
        }

        [Theory]
        [MemberData(nameof(EmptyNodeSelector))]
        public void ParsesNoneNodeSelectorExperimentalOptions(string nodeSelector)
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse(nodeSelector)
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.False(parameters.NodeSelector.HasValue);
        }

        public static IEnumerable<object[]> EmptyNodeSelector =>
            new List<object[]>
            {
                new object[] { "{ nodeSelector: {  } }" },
                new object[] { "{ nodeSelector: null }" }
            };

        [Fact]
        public void ParsesSomeNodeSelectorExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ nodeSelector: { disktype: \"ssd\", gpu: \"true\" } }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.True(parameters.NodeSelector.HasValue);
            parameters.NodeSelector.ForEach(selector => Assert.Equal(2, selector.Count));
            parameters.NodeSelector.ForEach(selector => Assert.Equal("ssd", selector["disktype"]));
            parameters.NodeSelector.ForEach(selector => Assert.Equal("true", selector["gpu"]));
        }
    }
}
