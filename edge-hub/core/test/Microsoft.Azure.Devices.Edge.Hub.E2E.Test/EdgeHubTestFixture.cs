// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using Xunit;

    /// <summary>
    /// Base test class for use by E2E tests.
    /// </summary>
    public class EdgeHubTestFixture
    {
        static readonly object FixtureLock = new object();
        static ProtocolHeadFixture protocolHeadFixtureInstance;

        public EdgeHubTestFixture()
        {
            if (protocolHeadFixtureInstance == null)
            {
                lock (FixtureLock)
                {
                    if (protocolHeadFixtureInstance == null)
                    {
                        protocolHeadFixtureInstance = EdgeHubFixtureCollection.GetFixture();
                    }
                }
            }
        }
    }
}
