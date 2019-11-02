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
            ProtocolHeadFixture fixture = new ProtocolHeadFixture();
            this.fixtures.Add(fixture);
            return fixture;
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
