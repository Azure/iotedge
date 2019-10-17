// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using Xunit;

    /// <summary>
    /// Base test class for use by E2E tests.
    /// </summary>
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    public class EdgeHubTest
    {
        static readonly object FixtureLock = new object();
        static ProtocolHeadFixture protocolHeadFixtureInstance;

        protected EdgeHubFixture edgeHubFixture;

        public EdgeHubTest()
        {
        }

        public EdgeHubTest(EdgeHubFixture edgeHubFixture)
        {
            this.edgeHubFixture = edgeHubFixture;

            if (protocolHeadFixtureInstance == null)
            {
                lock (FixtureLock)
                {
                    if (protocolHeadFixtureInstance == null)
                    {
                        protocolHeadFixtureInstance = this.edgeHubFixture.GetFixture();
                    }
                }
            }
        }
    }
}
