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
            string edgeDeviceHostname = "edgedevicehostname";
            string parentEdgeHostname = "parentedgehostname";
            string iothubHostname = "iothub.local";
            string deviceId = "device1";
            string moduleName = "module1";

            Assert.Throws<ArgumentException>(() => new ModuleIdentity(null, edgeDeviceHostname, parentEdgeHostname, deviceId, moduleName, new ConnectionStringCredentials(connectionString)));
            Assert.NotNull(new ModuleIdentity(iothubHostname, edgeDeviceHostname, null, deviceId, moduleName, new ConnectionStringCredentials(connectionString)));
            Assert.Throws<ArgumentException>(() => new ModuleIdentity(iothubHostname, edgeDeviceHostname, parentEdgeHostname, null, moduleName, new ConnectionStringCredentials(connectionString)));
            Assert.Throws<ArgumentException>(() => new ModuleIdentity(iothubHostname, edgeDeviceHostname, parentEdgeHostname, deviceId, null, new ConnectionStringCredentials(connectionString)));
            Assert.Throws<ArgumentNullException>(() => new ModuleIdentity(iothubHostname, edgeDeviceHostname, parentEdgeHostname, deviceId, moduleName, null));
        }
    }
}
