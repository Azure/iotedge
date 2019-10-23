// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using static Microsoft.Azure.Devices.Edge.Hub.E2E.Test.ProtocolHeadFixture;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    public class StoreLimitsTest : StoreLimitsTestBase
    {
        public StoreLimitsTest() : base(false)
        {
        }

        [Fact]
        async Task StoreLimitValidationTest()
        {
            await base.StoreLimitValidationTestAsync();
        }
    }
}
