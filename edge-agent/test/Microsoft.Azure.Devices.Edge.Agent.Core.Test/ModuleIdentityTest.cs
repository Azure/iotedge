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
            string iothubHostname = "iothub.local";
            string deviceId = "device1";
            string moduleName = "module1";

            Assert.Throws<ArgumentException>(() => new ModuleIdentity(null, deviceId, moduleName, new ConnectionStringCredentials(connectionString)));
            Assert.NotNull(new ModuleIdentity(iothubHostname, deviceId, moduleName, new ConnectionStringCredentials(connectionString)));
            Assert.Throws<ArgumentException>(() => new ModuleIdentity(iothubHostname, null, moduleName, new ConnectionStringCredentials(connectionString)));
            Assert.Throws<ArgumentException>(() => new ModuleIdentity(iothubHostname, deviceId, null, new ConnectionStringCredentials(connectionString)));
            Assert.Throws<ArgumentNullException>(() => new ModuleIdentity(iothubHostname, deviceId, moduleName, null));
        }
    }
}
