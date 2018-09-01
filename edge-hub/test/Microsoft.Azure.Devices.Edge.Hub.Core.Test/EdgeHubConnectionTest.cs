// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class EdgeHubConnectionTest
    {
        [Theory]
        [InlineData("1.0", null)]
        [InlineData("1.1", null)]
        [InlineData("1.2", null)]
        [InlineData("1.3", null)]
        [InlineData("1", typeof(ArgumentException))]
        [InlineData("", typeof(ArgumentException))]
        [InlineData(null, typeof(ArgumentException))]
        [InlineData("0.1", typeof(InvalidOperationException))]
        [InlineData("2.0", typeof(InvalidOperationException))]
        [InlineData("2.1", typeof(InvalidOperationException))]
        public void SchemaVersionCheckTest(string schemaVersion, Type expectedException)
        {
            if (expectedException != null)
            {
                Assert.Throws(expectedException, () => EdgeHubConnection.ValidateSchemaVersion(schemaVersion));
            }
            else
            {
                EdgeHubConnection.ValidateSchemaVersion(schemaVersion);
            }
        }
    }
}
