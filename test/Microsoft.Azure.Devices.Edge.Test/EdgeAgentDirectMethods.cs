// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Newtonsoft.Json;
    using NUnit.Framework;

    using ConfigModuleName = Microsoft.Azure.Devices.Edge.Test.Common.Config.ModuleName;

    [EndToEnd]
    public class EdgeAgentDirectMethods : SasManualProvisioningFixture
    {
        [Test]
        public async Task ValidateMetrics()
        {
            CancellationToken token = this.TestToken;
            await this.runtime.DeployConfigurationAsync(token);

            var result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeHub, new CloudToDeviceMethod("Ping"), token);

            Assert.AreEqual(result.Status, (int)HttpStatusCode.OK);
            Assert.AreEqual(result.GetPayloadAsJson(), null);
        }
    }
}
