// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using Xunit;

    [CollectionDefinition("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    public class EdgeHubCollection : ICollectionFixture<EdgeHubTestFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
