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
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using NUnit.Framework;

    using ConfigModuleName = Microsoft.Azure.Devices.Edge.Test.Common.Config.ModuleName;

    [EndToEnd]
    public class EdgeAgentDirectMethods : SasManualProvisioningFixture
    {
        [Test]
        [Category("nestededge_isa95")]
        public async Task TestPing()
        {
            CancellationToken token = this.TestToken;

            // This is a temporary solution see ticket: 9288683
            if (!Context.Current.ISA95Tag)
            {
                await this.runtime.DeployConfigurationAsync(this.cli, token, Context.Current.NestedEdge);
            }

            var result = await this.IotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("Ping", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);
            Assert.AreEqual("null", result.GetPayloadAsJson());
        }

        [Test]
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
                },
                this.cli,
                token,
                Context.Current.NestedEdge);
            await Task.Delay(30000);

            // Verify RFC3339 Operation
            string since = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm:ssZ");
            string until = DateTime.Now.AddDays(+1).ToString("yyyy-MM-dd'T'HH:mm:ssZ");

            var request = new ModuleLogsRequest("1.0", new List<LogRequestItem> { new LogRequestItem(moduleName, new ModuleLogFilter(Option.Some(10), Option.Some(since), Option.Some(until), Option.None<int>(), Option.None<bool>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text);

            var result = await this.IotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(request)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);

            string expected = string.Join('\n', Enumerable.Range(0, count)) + "\n";
            LogResponse response = JsonConvert.DeserializeObject<LogResponse[]>(result.GetPayloadAsJson()).Single();
            Assert.AreEqual(expected, response.Payload);

            // Verify Unix Time Operation
            since = DateTime.Now.AddDays(-1).ToUnixTimestamp().ToString();
            until = DateTime.Now.AddDays(+1).ToUnixTimestamp().ToString();

            request = new ModuleLogsRequest("1.0", new List<LogRequestItem> { new LogRequestItem(moduleName, new ModuleLogFilter(Option.Some(10), Option.Some(since), Option.Some(until), Option.None<int>(), Option.None<bool>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text);

            result = await this.IotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(request)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);

            expected = string.Join('\n', Enumerable.Range(0, count)) + "\n";
            response = JsonConvert.DeserializeObject<LogResponse[]>(result.GetPayloadAsJson()).Single();
            Assert.AreEqual(expected, response.Payload);

            // Verify Human Readable Time Operation
            since = "1 hour".ToString();
            until = "1 second".ToString();

            request = new ModuleLogsRequest("1.0", new List<LogRequestItem> { new LogRequestItem(moduleName, new ModuleLogFilter(Option.Some(10), Option.Some(since), Option.Some(until), Option.None<int>(), Option.None<bool>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text);

            result = await this.IotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(request)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);

            expected = string.Join('\n', Enumerable.Range(0, count)) + "\n";
            response = JsonConvert.DeserializeObject<LogResponse[]>(result.GetPayloadAsJson()).Single();
            Assert.AreEqual(expected, response.Payload);

            // Verify Incorrect Timestamp gives correct error
            since = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm");
            until = DateTime.Now.AddDays(+1).ToString("yyyy-MM-dd'T'HH:mm");

            request = new ModuleLogsRequest("1.0", new List<LogRequestItem> { new LogRequestItem(moduleName, new ModuleLogFilter(Option.Some(10), Option.Some(since), Option.Some(until), Option.None<int>(), Option.None<bool>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text);

            result = await this.IotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(request)), token);
            Assert.AreEqual((int)HttpStatusCode.BadRequest, result.Status);
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
                },
                this.cli,
                token,
                Context.Current.NestedEdge);
            await Task.Delay(30000);

            var request = new ModuleLogsRequest("1.0", new List<LogRequestItem> { new LogRequestItem(moduleName, new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.None<bool>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text);

            var result = await this.IotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(request)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);

            string expected = string.Join('\n', Enumerable.Range(0, count)) + "\n";
            LogResponse response = JsonConvert.DeserializeObject<LogResponse[]>(result.GetPayloadAsJson()).Single();
            Assert.AreEqual(expected, response.Payload);
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
                },
                this.cli,
                token,
                Context.Current.NestedEdge);
            await Task.Delay(10000);

            // restart module
            var restartRequest = new RestartRequest("1.0", moduleName);
            var result = await this.IotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("RestartModule", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(restartRequest)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);
            Assert.AreEqual("null", result.GetPayloadAsJson());
            await Task.Delay(10000);

            // check it restarted
            var logsRequest = new ModuleLogsRequest("1.0", new List<LogRequestItem> { new LogRequestItem(moduleName, new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.None<bool>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text);
            result = await this.IotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(logsRequest)), token);
            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);

            // The log _should_ contain two runs of 0-9, but on slower systems the restart request occasionally times
            // out and Edge Agent internally retries, resulting in two restarts of the number logger module. The
            // resulting logs might contain three sequences of numbers instead of two. To handle the variance we'll
            // look for minimal evidence of a restart (i.e. one full sequence followed by at least one number from the
            // next sequence) rather than expecting exactly two sequences.
            string expected = string.Join('\n', Enumerable.Range(0, count).Concat(Enumerable.Range(0, 1))) + "\n";
            LogResponse response = JsonConvert.DeserializeObject<LogResponse[]>(result.GetPayloadAsJson()).Single();
            Assert.That(response.Payload.StartsWith(expected));
        }

        [Test]
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
                },
                this.cli,
                token,
                Context.Current.NestedEdge);
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

            CloudToDeviceMethodResult result = await this.IotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("UploadModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(payload), token);

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
                },
                this.cli,
                token,
                Context.Current.NestedEdge);
            await Task.Delay(10000);

            var request = new
            {
                schemaVersion = "1.0",
                since = "5m",
                edgeRuntimeOnly = false,
                sasUrl,
            };

            var payload = JsonConvert.SerializeObject(request);

            CloudToDeviceMethodResult result = await this.IotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("UploadSupportBundle", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(payload), token);

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

                var result = await this.IotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetTaskStatus", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(request)), token);

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
