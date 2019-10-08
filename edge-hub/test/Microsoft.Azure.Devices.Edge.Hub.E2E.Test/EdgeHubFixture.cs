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
            Dispose(false);
        }

        internal ProtocolHeadFixture GetFixture()
        {
            ProtocolHeadFixture fixture = new ProtocolHeadFixture();
            this.fixtures.Add(fixture);
            return fixture;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            Console.WriteLine("Dispose edge hub fix");
            if (disposed)
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

            disposed = true;
        }
    }
}
