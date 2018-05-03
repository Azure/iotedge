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
            ModuleConnectionString.ModuleConnectionStringBuilder builder = new ModuleConnectionString.ModuleConnectionStringBuilder(iotHubHostName, deviceId).WithModuleId(moduleId);

            if (!string.IsNullOrEmpty(sasKey))
            {
                builder.WithSharedAccessKey(sasKey);
            }

            if (!string.IsNullOrEmpty(gatewayHostName))
            {
                builder.WithGatewayHostName(gatewayHostName);
            }

            ModuleConnectionString moduleConnectionString = builder.Build();

            // Act
            string connectionString = moduleConnectionString.ToString();

            // Assert
            Assert.Equal(expectedConnectionString, connectionString);
        }

        [Fact]
        public void ImplicitOperatorTest()
        {
            // Arrange/Act
            ModuleConnectionString.ModuleConnectionStringBuilder builder = new ModuleConnectionString.ModuleConnectionStringBuilder("foo.azure.com", "device1").WithModuleId("module1");
            string connectionString = builder
                .WithGatewayHostName("localhost")
                .Build();

            Assert.Equal("HostName=foo.azure.com;DeviceId=device1;ModuleId=module1;GatewayHostName=localhost", connectionString);
        }

        [Fact]
        public void InvalidInputsTest()
        {
            Assert.Throws<ArgumentException>(() => new ModuleConnectionString.ModuleConnectionStringBuilder(null, "1"));
            Assert.Throws<ArgumentException>(() => new ModuleConnectionString.ModuleConnectionStringBuilder("", "1"));
            Assert.Throws<ArgumentException>(() => new ModuleConnectionString.ModuleConnectionStringBuilder("iothub", null));
            Assert.Throws<ArgumentException>(() => new ModuleConnectionString.ModuleConnectionStringBuilder("iothub", ""));

            var builder = new ModuleConnectionString.ModuleConnectionStringBuilder("foo.azure.com", "device1");
            Assert.Throws<ArgumentException>(() => builder.Build());
            Assert.Throws<ArgumentException>(() => builder.WithModuleId(null).Build());
            Assert.Throws<ArgumentException>(() => builder.WithGatewayHostName(null).Build());
            Assert.Throws<ArgumentException>(() => builder.WithSharedAccessKey(null).Build());
        }
    }
}
