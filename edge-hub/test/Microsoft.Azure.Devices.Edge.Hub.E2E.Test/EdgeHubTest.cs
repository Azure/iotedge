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
        static ProtocolHeadFixture ProtocolHeadFixtureInstance;

        protected EdgeHubFixture edgeHubFixture;

        public EdgeHubTest()
        {
        }

        public EdgeHubTest(EdgeHubFixture edgeHubFixture)
        {
            this.edgeHubFixture = edgeHubFixture;

            if (ProtocolHeadFixtureInstance == null)
            {
                lock (FixtureLock)
                {
                    if (ProtocolHeadFixtureInstance == null)
                    {
                        ProtocolHeadFixtureInstance = this.edgeHubFixture.GetFixture();
                    }
                }
            }
        }
    }
}
