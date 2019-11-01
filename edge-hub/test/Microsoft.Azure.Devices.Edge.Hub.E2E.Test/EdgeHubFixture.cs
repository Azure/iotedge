// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;

    public class EdgeHubFixture : IDisposable
    {
        readonly IList<ProtocolHeadFixture> fixtures = new List<ProtocolHeadFixture>();
        bool disposed = false;

        public EdgeHubFixture()
        {
        }

        ~EdgeHubFixture()
        {
            this.Dispose(false);
        }

        internal ProtocolHeadFixture GetFixture()
        {
            // Before creating a new fixture, we need to ensure that other ProtocolHeadFixture
            // instances are closed. This is because a ProtocolHeadFixture encapsulates a
            // socket connection over an address and only one usage of this address is allowed
            // at a time.
            foreach (ProtocolHeadFixture fixture in this.fixtures)
            {
                if (!fixture.IsClosed)
                {
                    fixture.CloseAsync().Wait();
                }
            }

            ProtocolHeadFixture newFixture = new ProtocolHeadFixture();
            this.fixtures.Add(newFixture);
            return newFixture;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                foreach (ProtocolHeadFixture fixture in this.fixtures)
                {
                    fixture.Dispose();
                }
            }

            this.disposed = true;
        }
    }
}
