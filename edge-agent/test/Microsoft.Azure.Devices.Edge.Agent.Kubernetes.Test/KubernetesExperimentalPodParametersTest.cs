// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Collections.Generic;
    using System.Linq;
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

            Assert.False(parameters.Volumes.HasValue);
            Assert.False(parameters.NodeSelector.HasValue);
            Assert.False(parameters.Resources.HasValue);
            Assert.False(parameters.SecurityContext.HasValue);
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

        [Fact]
        public void ParsesNoneVolumesExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ volumes: null }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.False(parameters.Volumes.HasValue);
        }

        [Fact]
        public void ParsesEmptyVolumesExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ volumes: [  ] }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.True(parameters.Volumes.HasValue);
            parameters.Volumes.ForEach(Assert.Empty);
        }

        [Fact]
        public void ParsesVolumesExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse(
                    @"{ 
                        ""volumes"": [
                          {
                            ""volume"": {
                              ""name"": ""ModuleA"",
                              ""configMap"": {
                                ""optional"": ""true"",
                                ""defaultMode"": 420,
                                ""items"": [{
                                    ""key"": ""config-file"",
                                    ""path"": ""config.yaml"",
                                    ""mode"": 420
                                }],
                                ""name"": ""module-config""
                              }
                            },
                            ""volumeMounts"": [
                              {
                                ""name"": ""module-config"",
                                ""mountPath"": ""/etc/module/config.yaml"",
                                ""mountPropagation"": ""None"",
                                ""readOnly"": ""true"",
                                ""subPath"": """" 
                              }
                            ]
                          }
                        ]
                    }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.True(parameters.Volumes.HasValue);
            var volumeSpec = parameters.Volumes.OrDefault().Single();

            Assert.True(volumeSpec.Volume.HasValue);
            volumeSpec.Volume.ForEach(volume => Assert.Equal("ModuleA", volume.Name));
            volumeSpec.Volume.ForEach(volume => Assert.Equal(true, volume.ConfigMap.Optional));
            volumeSpec.Volume.ForEach(volume => Assert.Equal(420, volume.ConfigMap.DefaultMode));
            volumeSpec.Volume.ForEach(volume => Assert.Equal(1, volume.ConfigMap.Items.Count));
            volumeSpec.Volume.ForEach(volume => Assert.Equal("config-file", volume.ConfigMap.Items[0].Key));
            volumeSpec.Volume.ForEach(volume => Assert.Equal("config.yaml", volume.ConfigMap.Items[0].Path));
            volumeSpec.Volume.ForEach(volume => Assert.Equal(420, volume.ConfigMap.Items[0].Mode));
            volumeSpec.Volume.ForEach(volume => Assert.Equal("module-config", volume.ConfigMap.Name));

            Assert.True(volumeSpec.VolumeMounts.HasValue);
            volumeSpec.VolumeMounts.ForEach(mounts => Assert.Equal(1, mounts.Count));
            volumeSpec.VolumeMounts.ForEach(mounts => Assert.Equal("module-config", mounts[0].Name));
            volumeSpec.VolumeMounts.ForEach(mounts => Assert.Equal("/etc/module/config.yaml", mounts[0].MountPath));
            volumeSpec.VolumeMounts.ForEach(mounts => Assert.Equal("None", mounts[0].MountPropagation));
            volumeSpec.VolumeMounts.ForEach(mounts => Assert.Equal(true, mounts[0].ReadOnlyProperty));
            volumeSpec.VolumeMounts.ForEach(mounts => Assert.Equal(string.Empty, mounts[0].SubPath));
        }

        [Fact]
        public void ParsesNoneSecurityContextExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ securityContext: null }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.False(parameters.SecurityContext.HasValue);
        }

        [Fact]
        public void ParsesEmptySecurityContextExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ securityContext: {  } }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.True(parameters.SecurityContext.HasValue);
            parameters.SecurityContext.ForEach(securityContext => Assert.Null(securityContext.RunAsGroup));
            parameters.SecurityContext.ForEach(securityContext => Assert.Null(securityContext.RunAsUser));
            parameters.SecurityContext.ForEach(securityContext => Assert.Null(securityContext.RunAsNonRoot));
            parameters.SecurityContext.ForEach(securityContext => Assert.Null(securityContext.Sysctls));
            parameters.SecurityContext.ForEach(securityContext => Assert.Null(securityContext.FsGroup));
            parameters.SecurityContext.ForEach(securityContext => Assert.Null(securityContext.SeLinuxOptions));
            parameters.SecurityContext.ForEach(securityContext => Assert.Null(securityContext.SupplementalGroups));
        }

        [Fact]
        public void ParsesSomeSecurityContextExperimentalOptions()
        {
            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse("{ securityContext: { runAsGroup: 1001, runAsUser: 1000, runAsNonRoot: true } }")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            Assert.True(parameters.SecurityContext.HasValue);
            parameters.SecurityContext.ForEach(securityContext => Assert.Equal(1001, securityContext.RunAsGroup));
            parameters.SecurityContext.ForEach(securityContext => Assert.Equal(1000, securityContext.RunAsUser));
            parameters.SecurityContext.ForEach(securityContext => Assert.Equal(true, securityContext.RunAsNonRoot));
            parameters.SecurityContext.ForEach(securityContext => Assert.Null(securityContext.Sysctls));
            parameters.SecurityContext.ForEach(securityContext => Assert.Null(securityContext.FsGroup));
            parameters.SecurityContext.ForEach(securityContext => Assert.Null(securityContext.SeLinuxOptions));
            parameters.SecurityContext.ForEach(securityContext => Assert.Null(securityContext.SupplementalGroups));
        }
    }
}
