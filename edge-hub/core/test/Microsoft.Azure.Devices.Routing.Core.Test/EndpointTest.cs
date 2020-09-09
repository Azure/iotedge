// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Test.Endpoints;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class EndpointTest : RoutingUnitTestBase
    {
        static readonly Endpoint Endpoint1 = new TestEndpoint("id1");
        static readonly Endpoint Endpoint2 = new TestEndpoint("id2");
        static readonly Endpoint Endpoint3 = new TestEndpoint("id1");

        [Fact]
        [Unit]
        public void TestEquality()
        {
            Assert.NotEqual(new TestEndpoint("id1"), new TestEndpoint("id2"));
            Assert.Equal(new TestEndpoint("id1", "name1", "hub1"), new TestEndpoint("id1", "name2", "hub1"));
            Assert.Equal(new TestEndpoint("id1", "name1", "hub1"), new TestEndpoint("id1", "name1", "hub2"));
            Assert.Equal(Endpoint1, Endpoint1);
            Assert.Equal(Endpoint1, Endpoint3);
            Assert.NotEqual(Endpoint1, Endpoint2);
            Assert.NotEqual(Endpoint2, Endpoint3);
            Assert.NotNull(Endpoint2);

            Assert.False(Endpoint1.Equals(null));
            Assert.False(Endpoint1.Equals((object)null));
            Assert.True(Endpoint1.Equals((object)Endpoint1));
            Assert.False(Endpoint1.Equals((object)Endpoint2));
            Assert.False(Endpoint1.Equals(new object()));
        }

        [Fact]
        [Unit]
        public void TestHashCode()
        {
            ISet<Endpoint> endpoints = new HashSet<Endpoint>
            {
                Endpoint1,
                Endpoint2,
                Endpoint3
            };
            Assert.Equal(2, endpoints.Count);
            Assert.Contains(Endpoint1, endpoints);
            Assert.Contains(Endpoint2, endpoints);
        }

        [Fact]
        [Unit]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new TestEndpoint(null));
        }
    }
}
