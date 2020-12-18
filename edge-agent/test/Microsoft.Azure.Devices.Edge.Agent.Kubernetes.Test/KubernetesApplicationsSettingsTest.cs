// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class KubernetesApplicationSettingsTest
    {
        [Fact]
        public void NullSettingsYieldNoneResourceRequirements()
        {
            var appSettings = new KubernetesApplicationSettings
            {
                ProxyResourceRequests = null,
                AgentResourceRequests = null,
            };

            var proxyReqs = appSettings.GetProxyResourceRequirements();
            var agentReqs = appSettings.GetAgentResourceRequirements();

            Assert.False(proxyReqs.HasValue);
            Assert.False(agentReqs.HasValue);
        }

        [Fact]
        public void NonNullSettingsYieldResourceRequirements()
        {
            var resources = new ResourceSettings
            {
                Limits = new Dictionary<string, string>(),
                Requests = new Dictionary<string, string>(),
            };
            var appSettings = new KubernetesApplicationSettings
            {
                ProxyResourceRequests = resources,
                AgentResourceRequests = resources,
            };

            var proxyReqs = appSettings.GetProxyResourceRequirements();
            var agentReqs = appSettings.GetAgentResourceRequirements();

            Assert.True(proxyReqs.HasValue);
            Assert.True(agentReqs.HasValue);
        }
    }
}