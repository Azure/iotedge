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
        [Category("Flaky")]
        public async Task TestGetModuleLogs()
        {
            string moduleName = "NumberLogger";
            int count = 10;

            CancellationToken token = this.TestToken;

            string numberLoggerImage = Context.Current.NumberLoggerImage.Expect(() => new InvalidOperationException("Missing Number Logger image"));
            await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(moduleName, numberLoggerImage)
                        .WithEnvironment(new[] { ("Count", count.ToString()) });
                }, token);
            await Task.Delay(30000);

            string since = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm:sZ");
            string until = DateTime.Now.AddDays(+1).ToString("yyyy-MM-dd'T'HH:mm:sZ");

            var request = new ModuleLogsRequest("1.0", new List<LogRequestItem> { new LogRequestItem(moduleName, new ModuleLogFilter(Option.Some(10), Option.Some(since), Option.Some(until), Option.None<int>(), Option.None<bool>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text);

            var result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(request)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);

            string expected = string.Join('\n', Enumerable.Range(0, count)) + "\n";
            LogResponse response = JsonConvert.DeserializeObject<LogResponse[]>(result.GetPayloadAsJson()).Single();
            Assert.AreEqual(expected, response.Payload.Replace("\r\n", "\n"));
        }

        [Test]
        public async Task TestGetModuleLogsNo500Tail()
        {
            string moduleName = "NumberLogger";
            int count = 1000;

            CancellationToken token = this.TestToken;

            string numberLoggerImage = Context.Current.NumberLoggerImage.Expect(() => new InvalidOperationException("Missing Number Logger image"));
            await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(moduleName, numberLoggerImage)
                        .WithEnvironment(new[] { ("Count", count.ToString()) });
                }, token);
            await Task.Delay(30000);

            var request = new ModuleLogsRequest("1.0", new List<LogRequestItem> { new LogRequestItem(moduleName, new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.None<bool>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text);

            var result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(request)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);

            string expected = string.Join('\n', Enumerable.Range(0, count)) + "\n";
            LogResponse response = JsonConvert.DeserializeObject<LogResponse[]>(result.GetPayloadAsJson()).Single();
            Assert.AreEqual(expected, response.Payload.Replace("\r\n", "\n"));
        }

        [Test]
        public async Task TestRestartModule()
        {
            string moduleName = "NumberLogger";
            int count = 10;

            CancellationToken token = this.TestToken;

            string numberLoggerImage = Context.Current.NumberLoggerImage.Expect(() => new InvalidOperationException("Missing Number Logger image"));
            await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(moduleName, numberLoggerImage)
                        .WithEnvironment(new[] { ("Count", count.ToString()) });
                }, token);
            await Task.Delay(10000);

            // restart module
            var restartRequest = new RestartRequest("1.0", moduleName);
            var result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("RestartModule", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(restartRequest)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);
            Assert.AreEqual("null", result.GetPayloadAsJson());
            await Task.Delay(10000);

            // check it restarted
            var logsRequest = new ModuleLogsRequest("1.0", new List<LogRequestItem> { new LogRequestItem(moduleName, new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.None<bool>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text);
            result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(logsRequest)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);

            string expected = string.Join('\n', Enumerable.Range(0, count).Concat(Enumerable.Range(0, count))) + "\n";
            LogResponse response = JsonConvert.DeserializeObject<LogResponse[]>(result.GetPayloadAsJson()).Single();
            Assert.AreEqual(expected, response.Payload.Replace("\r\n", "\n"));
        }

        [Test]
        [Category("Flaky")]
        public async Task TestUploadModuleLogs()
        {
            string moduleName = "NumberLogger";
            int count = 10;
            string sasUrl = Context.Current.BlobSasUrl.Expect(() => new InvalidOperationException("Missing Blob SAS url"));

            CancellationToken token = this.TestToken;

            string numberLoggerImage = Context.Current.NumberLoggerImage.Expect(() => new InvalidOperationException("Missing Number Logger image"));
            await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(moduleName, numberLoggerImage)
                        .WithEnvironment(new[] { ("Count", count.ToString()) });
                }, token);
            await Task.Delay(10000);

            var request = new
            {
                schemaVersion = "1.0",
                items = new
                {
                    id = "NumberLogger",
                    filter = new { },
                },
                encoding = 0,
                contentYtpe = 1,
                sasUrl,
            };

            var payload = JsonConvert.SerializeObject(request);

            CloudToDeviceMethodResult result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("UploadModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(payload), token);

            var response = JsonConvert.DeserializeObject<TaskStatusResponse>(result.GetPayloadAsJson());
            await this.WaitForTaskCompletion(response.CorrelationId, token);
        }

        [Test]
        public async Task TestUploadSupportBundle()
        {
            string moduleName = "NumberLogger";
            int count = 10;
            string sasUrl = Context.Current.BlobSasUrl.Expect(() => new InvalidOperationException("Missing Blob SAS url"));

            CancellationToken token = this.TestToken;

            string numberLoggerImage = Context.Current.NumberLoggerImage.Expect(() => new InvalidOperationException("Missing Number Logger image"));
            await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(moduleName, numberLoggerImage)
                        .WithEnvironment(new[] { ("Count", count.ToString()) });
                }, token);
            await Task.Delay(10000);

            var request = new
            {
                schemaVersion = "1.0",
                sasUrl,
            };

            var payload = JsonConvert.SerializeObject(request);

            CloudToDeviceMethodResult result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("UploadSupportBundle", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(payload), token);

            var response = JsonConvert.DeserializeObject<TaskStatusResponse>(result.GetPayloadAsJson());
            await this.WaitForTaskCompletion(response.CorrelationId, token);
        }

        async Task WaitForTaskCompletion(string correlationId, CancellationToken token)
        {
            while (true)
            {
                var request = new
                {
                    schemaVersion = "1.0",
                    correlationId
                };

                var result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetTaskStatus", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(request)), token);

                Assert.AreEqual((int)HttpStatusCode.OK, result.Status);
                var response = JsonConvert.DeserializeObject<TaskStatusResponse>(result.GetPayloadAsJson());

                if (response.Status != BackgroundTaskRunStatus.NotStarted && response.Status != BackgroundTaskRunStatus.Running)
                {
                    Assert.AreEqual(BackgroundTaskRunStatus.Completed, response.Status, response.Message);
                    return;
                }

                await Task.Delay(5000);
            }
        }

        class LogResponse
        {
            [JsonProperty("payload")]
            public string Payload { get; set; }
        }
    }
}
