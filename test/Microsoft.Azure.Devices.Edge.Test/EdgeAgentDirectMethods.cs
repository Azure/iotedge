// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Newtonsoft.Json;
    using NUnit.Framework;

    using ConfigModuleName = Microsoft.Azure.Devices.Edge.Test.Common.Config.ModuleName;

    [EndToEnd]
    public class EdgeAgentDirectMethods : SasManualProvisioningFixture
    {
        [Test]
        public async Task TestPing()
        {
            CancellationToken token = this.TestToken;
            await this.runtime.DeployConfigurationAsync(token);

            var result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("Ping", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);
            Assert.AreEqual("null", result.GetPayloadAsJson());
        }

        [Test]
        public async Task TestModuleLogs()
        {
            CancellationToken token = this.TestToken;
            await this.runtime.DeployConfigurationAsync(token);

            string module = "agent";

            var request = new ModuleLogsRequest("1.0", new List<LogRequestItem> { new LogRequestItem(module, new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text);

            var result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("UploadModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(request)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);
            Assert.AreEqual("logs", result.GetPayloadAsJson());
        }
    }
}
