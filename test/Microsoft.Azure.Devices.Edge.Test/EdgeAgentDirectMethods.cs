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

            var request = new ModuleLogsRequest("1.0", new List<LogRequestItem> { new LogRequestItem(moduleName, new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text);

            var result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(request)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);

            string expected = string.Join('\n', Enumerable.Range(0, count).Concat(Enumerable.Range(0, count)));
            LogResponse response = JsonConvert.DeserializeObject<LogResponse[]>(result.GetPayloadAsJson()).Single();
            Assert.AreEqual("logs", response.Payload);
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

            // restart module
            var restartRequest = new RestartRequest("1.0", moduleName);
            var result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("RestartModule", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(restartRequest)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);
            Assert.AreEqual("null", result.GetPayloadAsJson());

            // check it restarted
            var logsRequest = new ModuleLogsRequest("1.0", new List<LogRequestItem> { new LogRequestItem(moduleName, new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text);
            result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(logsRequest)), token);

            Assert.AreEqual((int)HttpStatusCode.OK, result.Status);

            string expected = string.Join('\n', Enumerable.Range(0, count).Concat(Enumerable.Range(0, count)));
            LogResponse response = JsonConvert.DeserializeObject<LogResponse[]>(result.GetPayloadAsJson()).Single();
            Assert.AreEqual("logs", response.Payload);
        }

        [Test]
        public async Task TestUploadModuleLogs()
        {
            string moduleName = "NumberLogger";
            int count = 1000;
            string sasUrl = "https://lefitcheblobtest1.blob.core.windows.net/upload-test?sv=2019-02-02&st=2020-08-03T17%3A14%3A16Z&se=2020-11-04T18%3A14%3A00Z&sr=c&sp=racwdl&sig=phKgqaaxSJTcZzUcggE%2FnhDljs4%2BhvCg7IOKk8iWTcY%3D";

            CancellationToken token = this.TestToken;

            string numberLoggerImage = Context.Current.NumberLoggerImage.Expect(() => new InvalidOperationException("Missing Number Logger image"));
            await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(moduleName, numberLoggerImage)
                        .WithEnvironment(new[] { ("Count", count.ToString()) });
                }, token);

            var request = new ModuleLogsUploadRequest("1.0", new List<LogRequestItem> { new LogRequestItem(moduleName, new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.None<string>())) }, LogsContentEncoding.None, LogsContentType.Text, sasUrl);

            CloudToDeviceMethodResult result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("UploadModuleLogs", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(request)), token);

            TaskStatusResponse response = null;
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual((int)HttpStatusCode.OK, result.Status);
                response = JsonConvert.DeserializeObject<TaskStatusResponse>(result.GetPayloadAsJson());

                if (response.Status == BackgroundTaskRunStatus.NotStarted || response.Status == BackgroundTaskRunStatus.Running)
                {
                    break;
                }

                await Task.Delay(5000);
                var correlation = new TaskStatusRequest("1.0", response.CorrelationId);
                result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, ConfigModuleName.EdgeAgent, new CloudToDeviceMethod("GetTaskStatus", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)).SetPayloadJson(JsonConvert.SerializeObject(correlation)), token);
            }

            Assert.AreEqual(BackgroundTaskRunStatus.Completed, response.Status, response.Message);

            // BlobServiceClient blobServiceClient = new BlobServiceClient(sasUrl);
            // string expected = string.Join('\n', Enumerable.Range(0, count));
        }

        class LogResponse
        {
            [JsonProperty("payload")]
            public string Payload { get; set; }
        }
    }
}
