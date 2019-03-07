// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Xunit;

    [Unit]
    public class EdgeHubConfigTest
    {
        [Fact]
        public void ConstructorHappyPath()
        {
            // Arrange
            IEnumerable<(string Name, string Value, Route route)> routes = Enumerable.Empty<(string Name, string Value, Route route)>();
            var snfConfig = new StoreAndForwardConfiguration(1000);

            // Act
            var edgeHubConfig = new EdgeHubConfig("1.0", routes, snfConfig);

            // Assert
            Assert.NotNull(edgeHubConfig);
        }

        [Theory]
        [MemberData(nameof(GetConstructorInvalidParameters))]
        public void ConstructorInvalidParameters(string schemaVersion, IEnumerable<(string Name, string Value, Route Route)> routes, StoreAndForwardConfiguration configuration)
        {
            // Act & Assert
            Assert.ThrowsAny<ArgumentException>(() => new EdgeHubConfig(schemaVersion, routes, configuration));
        }

        public static IEnumerable<object[]> GetConstructorInvalidParameters()
        {
            yield return new object[] { null, new List<(string Name, string Value, Route route)>(), new StoreAndForwardConfiguration(1000) };
            yield return new object[] { "1.0", null, new StoreAndForwardConfiguration(1000) };
            yield return new object[] { "1.0", new List<(string Name, string Value, Route route)>(), null };
        }
    }
}
