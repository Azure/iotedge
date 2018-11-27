// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class TaskExTest
    {
        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            Task t1 = TaskEx.FromException(new InvalidOperationException("the message"));
            Assert.True(t1.IsFaulted);
            InvalidOperationException e1 = await Assert.ThrowsAsync<InvalidOperationException>(() => t1);
            Assert.Equal("the message", e1.Message);

            Task<int> t2 = TaskEx.FromException<int>(new BadImageFormatException("there's a bad image"));
            Assert.True(t1.IsFaulted);
            BadImageFormatException e2 = await Assert.ThrowsAsync<BadImageFormatException>(() => t2);
            Assert.Equal("there's a bad image", e2.Message);
        }

        [Fact]
        [Unit]
        public async Task WhenCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            await cts.Token.WhenCanceled();
        }

        [Fact]
        [Unit]
        public async Task WhenAllTuple()
        {
            (int val1, string val2) = await TaskEx.WhenAll(Task.FromResult(12), Task.FromResult("hello"));
            Assert.Equal(12, val1);
            Assert.Equal("hello", val2);

            (int a1, int a2, int a3, int a4, int a5, int a6, int a7, int a8) = await TaskEx.WhenAll(
                Task.FromResult(1),
                Task.FromResult(2),
                Task.FromResult(3),
                Task.FromResult(4),
                Task.FromResult(5),
                Task.FromResult(6),
                Task.FromResult(7),
                Task.FromResult(8));
            Assert.Equal(1, a1);
            Assert.Equal(2, a2);
            Assert.Equal(3, a3);
            Assert.Equal(4, a4);
            Assert.Equal(5, a5);
            Assert.Equal(6, a6);
            Assert.Equal(7, a7);
            Assert.Equal(8, a8);
        }

        [Fact]
        [Unit]
        public async Task ExecuteFuncUntilCancelledTest()
        {
            int TestFunc()
            {
                DateTime end = DateTime.Now + TimeSpan.FromSeconds(5);
                while (DateTime.Now < end)
                {
                    // No-op
                }

                return 0;
            }

            Func<int> operation = () => TestFunc();
            var cts = new CancellationTokenSource();
            Func<Task> testCode = () => operation.ExecuteUntilCancelled(cts.Token);
            Task assertTask = Assert.ThrowsAsync<TaskCanceledException>(testCode);

            await Task.Delay(TimeSpan.FromSeconds(2));
            cts.Cancel();

            await assertTask;

            // Assert
            Assert.True(assertTask.IsCompletedSuccessfully);
        }

        [Fact]
        [Unit]
        public async Task ExecuteActionUntilCancelledTest()
        {
            void TestAction()
            {
                DateTime end = DateTime.Now + TimeSpan.FromSeconds(5);
                while (DateTime.Now < end)
                {
                    // No-op
                }
            }

            Action operation = () => TestAction();
            var cts = new CancellationTokenSource();
            Func<Task> testCode = () => operation.ExecuteUntilCancelled(cts.Token);

            Task assertTask = Assert.ThrowsAsync<TaskCanceledException>(testCode);

            await Task.Delay(TimeSpan.FromSeconds(2));
            cts.Cancel();

            await assertTask;

            // Assert
            Assert.True(assertTask.IsCompletedSuccessfully);
        }

        [Fact]
        [Unit]
        public async Task ActionTimeoutAfterTest()
        {
            Task TestAction(CancellationToken _) => Task.Delay(TimeSpan.FromSeconds(10));
            Func<CancellationToken, Task> operation = c => TestAction(c);
            var cts = new CancellationTokenSource();
            Func<CancellationToken, Task> testCode = c => operation.TimeoutAfter(cts.Token, TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => testCode(cts.Token));
        }

        [Fact]
        [Unit]
        public async Task FuncTimeoutAfterTest()
        {
            async Task<int> TestFunc(CancellationToken _)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                return 10;
            }

            Func<CancellationToken, Task<int>> operation = c => TestFunc(c);
            var cts = new CancellationTokenSource();
            Func<CancellationToken, Task> testCode = c => operation.TimeoutAfter(cts.Token, TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => testCode(cts.Token));
        }

        [Fact]
        [Unit]
        public async Task ActionTimeoutAfterCancelTest()
        {
            Task TestAction(CancellationToken _) => Task.Delay(TimeSpan.FromSeconds(10));
            Func<CancellationToken, Task> operation = c => TestAction(c);
            var cts = new CancellationTokenSource();
            Func<CancellationToken, Task> testCode = c => operation.TimeoutAfter(cts.Token, TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => testCode(cts.Token));
        }

        [Fact]
        [Unit]
        public async Task FuncTimeoutAfterCancelTest()
        {
            async Task<int> TestFunc(CancellationToken _)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                return 10;
            }

            Func<CancellationToken, Task<int>> operation = c => TestFunc(c);
            var cts = new CancellationTokenSource();
            Func<CancellationToken, Task> testCode = c => operation.TimeoutAfter(cts.Token, TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => testCode(cts.Token));
        }
    }
}
