// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    public class InMemoryDbStoreLimitsTest : StoreLimitsTestBase
    {
        public InMemoryDbStoreLimitsTest(EdgeHubFixture edgeHubFixture)
            : base(edgeHubFixture, false)
        {
        }

        [Fact]
        async Task InMemoryDbStoreLimitValidationTest() => await this.StoreLimitValidationTestAsync();
    }
}
