// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Requests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class TaskStatusRequestHandlerTest
    {
        [Fact]
        public async Task SmokeTest()
        {
            async Task TestTask()
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }

            (string correlationId, BackgroundTaskStatus backgroundTaskStatus) = BackgroundTask.Run(TestTask, "test", CancellationToken.None);

            string payload = @"{
                    ""schemaVersion"": ""1.0"",
                    ""correlationId"": ""<correlationId>""
                }".Replace("<correlationId>", correlationId);
            var taskStatusRequestHandler = new TaskStatusRequestHandler();
            Option<string> response = await taskStatusRequestHandler.HandleRequest(Option.Some(payload), CancellationToken.None);

            Assert.True(response.HasValue);
            TaskStatusResponse taskStatusResponse = response.OrDefault().FromJson<TaskStatusResponse>();
            Assert.NotNull(taskStatusResponse);
            Assert.Equal(taskStatusResponse.CorrelationId, correlationId);
        }

        [Fact]
        public async Task InvalidInputsTest()
        {
            var taskStatusRequestHandler = new TaskStatusRequestHandler();
            await Assert.ThrowsAsync<ArgumentException>(() => taskStatusRequestHandler.HandleRequest(Option.None<string>(), CancellationToken.None));

            string payload1 = @"{
                    ""schemaVersion"": ""2.0"",
                    ""correlationId"": ""1234""
                }";
            await Assert.ThrowsAsync<InvalidSchemaVersionException>(() => taskStatusRequestHandler.HandleRequest(Option.Some(payload1), CancellationToken.None));

            string payload2 = @"{
                    ""schemaVersion"": ""1.0"",
                    ""correlationId"": """"
                }";
            await Assert.ThrowsAsync<ArgumentException>(() => taskStatusRequestHandler.HandleRequest(Option.Some(payload2), CancellationToken.None));
        }
    }
}
