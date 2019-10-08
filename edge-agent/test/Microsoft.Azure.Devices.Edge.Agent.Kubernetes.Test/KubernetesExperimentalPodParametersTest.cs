// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Collections.Generic;
    using k8s.Models;
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
            Assert.False(parameters.Resources.HasValue);
        }

        [Fact]
        public void ParsesNoneNodeSelectorExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ nodeSelector: null }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.False(parameters.NodeSelector.HasValue);
        }

        [Fact]
        public void ParsesEmptyNodeSelectorExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ nodeSelector: {  } }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.True(parameters.NodeSelector.HasValue);
            parameters.NodeSelector.ForEach(Assert.Empty);
        }

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

        [Fact]
        public void ParsesNoneResourcesExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ resources: null }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.False(parameters.Resources.HasValue);
        }

        [Fact]
        public void ParsesEmptyResourcesExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ resources: {  } }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.True(parameters.Resources.HasValue);
            parameters.Resources.ForEach(resources => Assert.Null(resources.Limits));
            parameters.Resources.ForEach(resources => Assert.Null(resources.Requests));
        }

        [Fact]
        public void ParsesEmptyRequirementsResourcesExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ resources: { limits: {}, requests: {} } }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.True(parameters.Resources.HasValue);
            parameters.Resources.ForEach(resources => Assert.Empty(resources.Limits));
            parameters.Resources.ForEach(resources => Assert.Empty(resources.Requests));
        }

        [Fact]
        public void ParsesSomeResourcesExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ resources: { limits: { \"memory\": \"128Mi\" }, requests: { \"cpu\": \"250m\" } } }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.True(parameters.Resources.HasValue);
            parameters.Resources.ForEach(resources => Assert.Equal(new Dictionary<string, ResourceQuantity> { ["memory"] = new ResourceQuantity("128Mi") }, resources.Limits));
            parameters.Resources.ForEach(resources => Assert.Equal(new Dictionary<string, ResourceQuantity> { ["cpu"] = new ResourceQuantity("250m") }, resources.Requests));
        }
    }
}
