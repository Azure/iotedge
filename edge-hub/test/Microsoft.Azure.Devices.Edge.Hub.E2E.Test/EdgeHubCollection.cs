using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    [CollectionDefinition("Microsoft.Azure.Devices.Edge.Hub.E2E.EdgeHubCollection.Test")]
    public class EdgeHubCollection : ICollectionFixture<EdgeHubFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
