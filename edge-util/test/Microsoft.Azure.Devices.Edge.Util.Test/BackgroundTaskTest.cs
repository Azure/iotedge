// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class BackgroundTaskTest
    {
        [Fact]
        public async Task SmokeTest()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            bool completed = false;
            async Task TestTask()
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                completed = true;
            }

            (string correlationId, BackgroundTaskStatus backgroundTaskStatus) = BackgroundTask.Run(TestTask, "testTask", cts.Token);

            Assert.NotEmpty(correlationId);
            Assert.Equal(BackgroundTaskRunStatus.Running, backgroundTaskStatus.Status);
            Assert.False(completed);
            Assert.False(backgroundTaskStatus.Exception.HasValue);

            await Task.Delay(TimeSpan.FromSeconds(5));
            backgroundTaskStatus = BackgroundTask.GetStatus(correlationId);

            Assert.Equal(BackgroundTaskRunStatus.Completed, backgroundTaskStatus.Status);
            Assert.True(completed);
            Assert.False(backgroundTaskStatus.Exception.HasValue);
        }

        [Fact]
        public async Task TaskFailedTest()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            var testException = new InvalidOperationException("foo");
            async Task TestTask()
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                throw testException;
            }

            (string correlationId, BackgroundTaskStatus backgroundTaskStatus) = BackgroundTask.Run(TestTask, "testTask", cts.Token);

            Assert.NotEmpty(correlationId);
            Assert.Equal(BackgroundTaskRunStatus.Running, backgroundTaskStatus.Status);
            Assert.False(backgroundTaskStatus.Exception.HasValue);

            await Task.Delay(TimeSpan.FromSeconds(6));
            backgroundTaskStatus = BackgroundTask.GetStatus(correlationId);

            Assert.Equal(BackgroundTaskRunStatus.Failed, backgroundTaskStatus.Status);
            Assert.True(backgroundTaskStatus.Exception.HasValue);
            Assert.Equal(testException, backgroundTaskStatus.Exception.OrDefault());
        }

        [Fact]
        public async Task MultipleTasksTest()
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            bool isCompleted1 = false;
            async Task TestTask1()
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
                isCompleted1 = true;
            }

            bool isCompleted2 = false;
            async Task TestTask2()
            {
                await Task.Delay(TimeSpan.FromSeconds(8), cts.Token);
                isCompleted2 = true;
            }

            bool isCompleted3 = false;
            async Task TestTask3()
            {
                await Task.Delay(TimeSpan.FromSeconds(14), cts.Token);
                isCompleted3 = true;
            }

            (string correlationId1, BackgroundTaskStatus backgroundTaskStatus1) = BackgroundTask.Run(TestTask1, "testTask1", cts.Token);
            (string correlationId2, BackgroundTaskStatus backgroundTaskStatus2) = BackgroundTask.Run(TestTask2, "testTask2", cts.Token);
            (string correlationId3, BackgroundTaskStatus backgroundTaskStatus3) = BackgroundTask.Run(TestTask3, "testTask3", cts.Token);

            Assert.NotEmpty(correlationId1);
            Assert.NotNull(backgroundTaskStatus1);
            Assert.Equal(BackgroundTaskRunStatus.Running, backgroundTaskStatus1.Status);
            Assert.False(backgroundTaskStatus1.Exception.HasValue);
            Assert.False(isCompleted1);

            Assert.NotEmpty(correlationId2);
            Assert.NotNull(backgroundTaskStatus2);
            Assert.Equal(BackgroundTaskRunStatus.Running, backgroundTaskStatus2.Status);
            Assert.False(backgroundTaskStatus2.Exception.HasValue);
            Assert.False(isCompleted2);

            Assert.NotEmpty(correlationId3);
            Assert.NotNull(backgroundTaskStatus3);
            Assert.Equal(BackgroundTaskRunStatus.Running, backgroundTaskStatus3.Status);
            Assert.False(backgroundTaskStatus3.Exception.HasValue);
            Assert.False(isCompleted3);

            await Task.Delay(TimeSpan.FromSeconds(5));

            backgroundTaskStatus1 = BackgroundTask.GetStatus(correlationId1);
            Assert.NotNull(backgroundTaskStatus1);
            Assert.Equal(BackgroundTaskRunStatus.Completed, backgroundTaskStatus1.Status);
            Assert.False(backgroundTaskStatus1.Exception.HasValue);
            Assert.True(isCompleted1);

            backgroundTaskStatus2 = BackgroundTask.GetStatus(correlationId2);
            Assert.NotNull(backgroundTaskStatus2);
            Assert.Equal(BackgroundTaskRunStatus.Running, backgroundTaskStatus2.Status);
            Assert.False(backgroundTaskStatus2.Exception.HasValue);
            Assert.False(isCompleted2);

            backgroundTaskStatus3 = BackgroundTask.GetStatus(correlationId3);
            Assert.NotNull(backgroundTaskStatus3);
            Assert.Equal(BackgroundTaskRunStatus.Running, backgroundTaskStatus3.Status);
            Assert.False(backgroundTaskStatus3.Exception.HasValue);
            Assert.False(isCompleted3);

            await Task.Delay(TimeSpan.FromSeconds(7));

            backgroundTaskStatus1 = BackgroundTask.GetStatus(correlationId1);
            Assert.NotNull(backgroundTaskStatus1);
            Assert.Equal(BackgroundTaskRunStatus.Completed, backgroundTaskStatus1.Status);
            Assert.False(backgroundTaskStatus1.Exception.HasValue);
            Assert.True(isCompleted1);

            backgroundTaskStatus2 = BackgroundTask.GetStatus(correlationId2);
            Assert.NotNull(backgroundTaskStatus2);
            Assert.Equal(BackgroundTaskRunStatus.Completed, backgroundTaskStatus2.Status);
            Assert.False(backgroundTaskStatus2.Exception.HasValue);
            Assert.True(isCompleted2);

            backgroundTaskStatus3 = BackgroundTask.GetStatus(correlationId3);
            Assert.NotNull(backgroundTaskStatus3);
            Assert.Equal(BackgroundTaskRunStatus.Running, backgroundTaskStatus3.Status);
            Assert.False(backgroundTaskStatus3.Exception.HasValue);
            Assert.False(isCompleted3);

            await Task.Delay(TimeSpan.FromSeconds(7));

            backgroundTaskStatus1 = BackgroundTask.GetStatus(correlationId1);
            Assert.NotNull(backgroundTaskStatus1);
            Assert.Equal(BackgroundTaskRunStatus.Completed, backgroundTaskStatus1.Status);
            Assert.False(backgroundTaskStatus1.Exception.HasValue);
            Assert.True(isCompleted1);

            backgroundTaskStatus2 = BackgroundTask.GetStatus(correlationId2);
            Assert.NotNull(backgroundTaskStatus2);
            Assert.Equal(BackgroundTaskRunStatus.Completed, backgroundTaskStatus2.Status);
            Assert.False(backgroundTaskStatus2.Exception.HasValue);
            Assert.True(isCompleted2);

            backgroundTaskStatus3 = BackgroundTask.GetStatus(correlationId3);
            Assert.NotNull(backgroundTaskStatus3);
            Assert.Equal(BackgroundTaskRunStatus.Completed, backgroundTaskStatus3.Status);
            Assert.False(backgroundTaskStatus3.Exception.HasValue);
            Assert.True(isCompleted3);

            BackgroundTaskStatus unknownTaskStatus = BackgroundTask.GetStatus(Guid.NewGuid().ToString());

            Assert.NotNull(unknownTaskStatus);
            Assert.Equal(BackgroundTaskRunStatus.Unknown, unknownTaskStatus.Status);
        }
    }
}
