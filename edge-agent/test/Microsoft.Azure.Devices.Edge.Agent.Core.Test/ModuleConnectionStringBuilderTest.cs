// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class ModuleConnectionStringBuilderTest
    {
        [Theory]
        [InlineData("HostName=foo.azure.com;DeviceId=d1;ModuleId=m1", "foo.azure.com", "d1", "m1")]
        [InlineData("HostName=foo.azure.com;DeviceId=d1;ModuleId=m1;SharedAccessKey=xyz", "foo.azure.com", "d1", "m1", "xyz")]
        [InlineData("HostName=foo.azure.com;DeviceId=d1;ModuleId=m1;SharedAccessKey=xyz;GatewayHostName=localhost", "foo.azure.com", "d1", "m1", "xyz", "localhost")]
        [InlineData("HostName=foo.azure.com;DeviceId=d1;ModuleId=m1;GatewayHostName=localhost", "foo.azure.com", "d1", "m1", null, "localhost")]
        public void CreateConnectionStringTest(string expectedConnectionString, string iotHubHostName, string deviceId, string moduleId, string sasKey = null, string gatewayHostName = null)
        {
            // Arrange
            var builder = new ModuleConnectionStringBuilder(iotHubHostName, deviceId);
            ModuleConnectionStringBuilder.ModuleConnectionString moduleConnectionString = builder.Create(moduleId);

            if (!string.IsNullOrEmpty(sasKey))
            {
                moduleConnectionString.WithSharedAccessKey(sasKey);
            }

            if (!string.IsNullOrEmpty(gatewayHostName))
            {
                moduleConnectionString.WithGatewayHostName(gatewayHostName);
            }

            // Act
            string connectionString = moduleConnectionString.Build();

            // Assert
            Assert.Equal(expectedConnectionString, connectionString);
        }

        [Fact]
        public void ImplicitOperatorTest()
        {
            // Arrange/Act
            var builder = new ModuleConnectionStringBuilder("foo.azure.com", "device1");
            string connectionString = builder.Create("module1")
                .WithGatewayHostName("localhost");

            Assert.Equal("HostName=foo.azure.com;DeviceId=device1;ModuleId=module1;GatewayHostName=localhost", connectionString);
        }

        [Fact]
        public void InvalidInputsTest()
        {
            Assert.Throws<ArgumentException>(() => new ModuleConnectionStringBuilder(null, "1"));
            Assert.Throws<ArgumentException>(() => new ModuleConnectionStringBuilder("", "1"));
            Assert.Throws<ArgumentException>(() => new ModuleConnectionStringBuilder("iothub", null));
            Assert.Throws<ArgumentException>(() => new ModuleConnectionStringBuilder("iothub", ""));

            var builder = new ModuleConnectionStringBuilder("foo.azure.com", "device1");
            Assert.Throws<ArgumentException>(() => builder.Create(null));
            Assert.Throws<ArgumentException>(() => builder.Create("m1").WithGatewayHostName(null));
            Assert.Throws<ArgumentException>(() => builder.Create("m1").WithSharedAccessKey(null));
        }
    }
}
