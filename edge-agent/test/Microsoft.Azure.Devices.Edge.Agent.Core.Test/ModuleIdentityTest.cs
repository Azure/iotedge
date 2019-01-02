// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class ModuleIdentityTest
    {
        [Fact]
        [Unit]
        public void TestCreateInstance_ShouldThrowWhithNullArguments()
        {
            string connectionString = "fake";
            string moduleName = "module1";
            string gatewayHostname = "gateway.local";
            string iothubHostname = "iothub.local";
            string deviceId = "device1";

            Assert.Throws<ArgumentException>(() => new ModuleIdentity(null, gatewayHostname, deviceId, moduleName, new ConnectionStringCredentials(connectionString)));
            Assert.NotNull(new ModuleIdentity(iothubHostname, null, deviceId, moduleName, new ConnectionStringCredentials(connectionString)));
            Assert.Throws<ArgumentException>(() => new ModuleIdentity(iothubHostname, gatewayHostname, null, moduleName, new ConnectionStringCredentials(connectionString)));
            Assert.Throws<ArgumentException>(() => new ModuleIdentity(iothubHostname, gatewayHostname, deviceId, null, new ConnectionStringCredentials(connectionString)));
            Assert.Throws<ArgumentNullException>(() => new ModuleIdentity(iothubHostname, gatewayHostname, deviceId, moduleName, null));
        }
    }
}
