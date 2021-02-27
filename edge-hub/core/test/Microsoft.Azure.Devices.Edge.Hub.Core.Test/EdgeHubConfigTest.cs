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
            yield return new object[]
            {
                null, new Dictionary<string,
                RouteConfig>(),
                new StoreAndForwardConfiguration(1000),
                new BrokerConfig()
            };
            yield return new object[]
            {
                "1.0",
                null,
                new StoreAndForwardConfiguration(1000),
                new BrokerConfig()
            };
            yield return new object[]
            {
                "1.0",
                new Dictionary<string, RouteConfig>(),
                null,
                new BrokerConfig()
            };
            yield return new object[]
            {
                "1.0",
                new Dictionary<string, RouteConfig>(),
                new StoreAndForwardConfiguration(1000),
                null
            };
        }

        [Theory]
        [MemberData(nameof(TwinIntegrityTestData))]
        public void ConstructorHappyPath(string[] signercert, string[] intermediatecacert, string signature, string algo)
        {
            // Arrange
            IReadOnlyDictionary<string, RouteConfig> routes = new ReadOnlyDictionary<string, RouteConfig>(new Dictionary<string, RouteConfig>());
            var snfConfig = new StoreAndForwardConfiguration(1000);
            var brokerConfig = new BrokerConfig();
            var integrity = new ManifestIntegrity(new TwinHeader(signercert, intermediatecacert), new TwinSignature(signature, algo));

            // Act
            var edgeHubConfig = new EdgeHubConfig("1.0", routes, snfConfig, Option.Some(brokerConfig), Option.Some(integrity));

            // Assert
            Assert.NotNull(edgeHubConfig);
        }

        public static IEnumerable<object[]> TwinIntegrityTestData()
        {
            yield return new object[] { null, null, "bytes", "algo" };
            yield return new object[] { new string[] { string.Empty, "signercert2" }, new string[] { string.Empty, "intermediatecacert1" }, "bytes", "algo" };
            yield return new object[] { new string[] { "signercert1", string.Empty }, new string[] { "intermediatecacert1", string.Empty }, "bytes", "algo" };
            yield return new object[] { new string[] { "signercert1", "signercert2" }, new string[] { "intermediatecacert1", "intermediatecacert2" }, "bytes", "algo" };
            yield return new object[] { new string[] { "signercert1", "signercert2" }, new string[] { "intermediatecacert1", "intermediatecacert2" }, string.Empty, "algo" };
            yield return new object[] { new string[] { "signercert1", "signercert2" }, new string[] { "intermediatecacert1", "intermediatecacert2" }, "bytes", string.Empty };
            yield return new object[] { new string[] { "signercert1", "signercert2" }, new string[] { "intermediatecacert1", "intermediatecacert2" }, string.Empty, string.Empty };
        }

        [Theory]
        [MemberData(nameof(GetConstructorInvalidParameters))]
        public void ConstructorInvalidParameters(
            string schemaVersion,
            Dictionary<string, RouteConfig> routes,
            StoreAndForwardConfiguration configuration,
            BrokerConfig brokerConfig)
        {
            // Act & Assert
            Assert.ThrowsAny<ArgumentException>(() => new EdgeHubConfig(schemaVersion, routes, configuration, Option.Some(brokerConfig), Option.None<ManifestIntegrity>()));
        }
    }
}
