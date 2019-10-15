// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using Xunit;

    /// <summary>
    /// Base test class for use by E2E tests.
    /// </summary>
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    public class EdgeHubTest : IDisposable
    {
        protected EdgeHubFixture edgeHubFixture;
        protected ProtocolHeadFixture initialFixture;

        public EdgeHubTest()
        {
        }

        public EdgeHubTest(EdgeHubFixture edgeHubFixture)
        {
            this.edgeHubFixture = edgeHubFixture;
            this.initialFixture = this.edgeHubFixture.GetFixture();
        }

        public virtual void Dispose()
        {
            this.initialFixture.CloseAsync().Wait();
        }
    }
}
