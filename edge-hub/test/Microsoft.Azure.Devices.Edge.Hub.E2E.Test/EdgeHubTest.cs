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
        static readonly object FixtureLock = new object();
        static ProtocolHeadFixture protocolHeadFixtureInstance;

        protected EdgeHubFixture edgeHubFixture;
        bool disposed = false;

        public EdgeHubTest()
        {
        }

        ~EdgeHubTest()
        {
            this.Dispose(false);
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

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                // Closing the protocol heads so that the HTTP protocol head port is freed up for other tests
                // to use that create their own ProtocolHeadFixture(s).
                // Ideally we should be calling the Dispose method on `protocolHeadFixtureInstance` here and make it
                // a non-static member of the test class but calling Dispose() on a `ProtocolHeadFixture` leads to
                // the disposal of static objects in the EdgeHub runtime which errors when a test attempts to
                // recreate a new ProtocolHeadFixture.
                // A static instance of `ProtocolHeadFixture` is being used here to avoid the cost of reconstructing
                // a new `ProtocolHeadFixture` for every test class when in most cases they actually don't care
                // about getting a new fixture or reusing an old one.
                protocolHeadFixtureInstance.CloseAsync().Wait();
            }

            this.disposed = true;
        }
    }
}
