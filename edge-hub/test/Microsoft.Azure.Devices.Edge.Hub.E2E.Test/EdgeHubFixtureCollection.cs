// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System.Collections.Generic;

    public static class EdgeHubFixtureCollection
    {
        static readonly object UpdateLock = new object();
        static readonly IList<ProtocolHeadFixture> Fixtures = new List<ProtocolHeadFixture>();

        internal static ProtocolHeadFixture GetFixture()
        {
            lock (UpdateLock)
            {
                // Before creating a new fixture, we need to ensure that other ProtocolHeadFixture
                // instances are closed. This is because a ProtocolHeadFixture encapsulates a
                // socket connection over an address and only one usage of this address is allowed
                // at a time.
                foreach (ProtocolHeadFixture fixture in Fixtures)
                {
                    if (!fixture.IsClosed)
                    {
                        fixture.CloseAsync().Wait();
                    }
                }

                ProtocolHeadFixture newFixture = new ProtocolHeadFixture();
                Fixtures.Add(newFixture);
                return newFixture;
            }
        }
    }
}
