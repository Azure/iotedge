// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class ResourceSettingsTest
    {
        static Dictionary<string, string> resourceSet1 = new Dictionary<string, string>
        {
            ["resource1"] = "100Mi",
            ["resource2"] = "200M",
            ["resource3"] = "3",
        };

        static Dictionary<string, string> resourceSet2 = new Dictionary<string, string>
        {
            ["resource4"] = "300Mi",
            ["resource5"] = "100M",
            ["resource6"] = "6",
        };

        static Dictionary<string, string> resourceSet3 = new Dictionary<string, string>();

        static ResourceQuantity rq1 = new ResourceQuantity("100Mi");
        static ResourceQuantity rq2 = new ResourceQuantity("200M");
        static ResourceQuantity rq3 = new ResourceQuantity("3");
        static ResourceQuantity rq4 = new ResourceQuantity("300Mi");
        static ResourceQuantity rq5 = new ResourceQuantity("100M");
        static ResourceQuantity rq6 = new ResourceQuantity("6");

        [Fact]
        public void NullSettingsYieldNullResourceRequirements()
        {
            var nullResource = new ResourceSettings
            {
                Limits = null,
                Requests = null,
            };

            V1ResourceRequirements resources = nullResource.ToResourceRequirements();

            Assert.NotNull(resources);
            Assert.Null(resources.Limits);
            Assert.Null(resources.Requests);
        }

        [Fact]
        public void EmptySettingsYieldEmptyResourceRequirements()
        {
            var nullResource = new ResourceSettings
            {
                Limits = resourceSet3,
                Requests = resourceSet3,
            };

            V1ResourceRequirements resources = nullResource.ToResourceRequirements();

            Assert.NotNull(resources);
            Assert.NotNull(resources.Limits);
            Assert.NotNull(resources.Requests);
            Assert.Equal(0, resources.Limits.Count);
            Assert.Equal(0, resources.Requests.Count);
        }

        [Fact]
        public void AssignedSettingsYieldResourceRequirements()
        {
            var nullResource = new ResourceSettings
            {
                Limits = resourceSet1,
                Requests = resourceSet2,
            };

            V1ResourceRequirements resources = nullResource.ToResourceRequirements();

            Assert.NotNull(resources);
            Assert.NotNull(resources.Limits);
            Assert.NotNull(resources.Requests);
            Assert.Equal(rq1, resources.Limits["resource1"]);
            Assert.Equal(rq2, resources.Limits["resource2"]);
            Assert.Equal(rq3, resources.Limits["resource3"]);
            Assert.Equal(rq4, resources.Requests["resource4"]);
            Assert.Equal(rq5, resources.Requests["resource5"]);
            Assert.Equal(rq6, resources.Requests["resource6"]);
        }
    }
}