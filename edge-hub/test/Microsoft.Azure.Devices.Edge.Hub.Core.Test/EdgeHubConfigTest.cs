// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class EdgeHubConfigTest
    {
        public static IEnumerable<object[]> GetConstructorInvalidParameters()
        {
            yield return new object[] { null, new Dictionary<string, RouteConfig>(), new StoreAndForwardConfiguration(1000, Option.Some(10L)) };
            yield return new object[] { "1.0", null, new StoreAndForwardConfiguration(1000) };
            yield return new object[] { "1.0", new Dictionary<string, RouteConfig>(), null };
        }

        [Fact]
        public void ConstructorHappyPath()
        {
            // Arrange
            IReadOnlyDictionary<string, RouteConfig> routes = new ReadOnlyDictionary<string, RouteConfig>(new Dictionary<string, RouteConfig>());
            var snfConfig = new StoreAndForwardConfiguration(1000, Option.Some(10L));

            // Act
            var edgeHubConfig = new EdgeHubConfig("1.0", routes, snfConfig);

            // Assert
            Assert.NotNull(edgeHubConfig);
        }

        [Theory]
        [MemberData(nameof(GetConstructorInvalidParameters))]
        public void ConstructorInvalidParameters(string schemaVersion, Dictionary<string, RouteConfig> routes, StoreAndForwardConfiguration configuration)
        {
            // Act & Assert
            Assert.ThrowsAny<ArgumentException>(() => new EdgeHubConfig(schemaVersion, routes, configuration));
        }
    }
}
