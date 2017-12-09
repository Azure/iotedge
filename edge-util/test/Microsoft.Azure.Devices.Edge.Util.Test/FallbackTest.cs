// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class FallbackTest
    {
        [Fact]
        public async Task FallbackInvokesPrimaryNotSecondary()
        {
            bool secondary = false;
            int result = await Fallback.ExecuteAsync(
                () => Task.FromResult(1),
                () => { secondary = true; return Task.FromResult(2); });
            Assert.Equal(1, result);
            Assert.False(secondary);
        }

        [Fact]
        public async Task FallbackInvokesSecondaryIfPrimaryThrows()
        {
            int result = await Fallback.ExecuteAsync(
                () => throw new Exception(),
                () => Task.FromResult(2));
            Assert.Equal(2, result);
        }

        class SecondaryException : Exception { }

        [Fact]
        public async Task FallbackThrowsIfSecondaryThrows()
        {
            Func<Task> test = async () => await Fallback.ExecuteAsync<int>(
                () => throw new Exception(),
                () => throw new SecondaryException());
            await Assert.ThrowsAsync<SecondaryException>(test);
        }

        [Fact]
        public async Task FallbackThrowsIfPrimaryThrowsFatalException()
        {
            Func<Task> test = async () => await Fallback.ExecuteAsync(
                () => throw new OutOfMemoryException(),
                () => Task.FromResult(2));
            await Assert.ThrowsAsync<OutOfMemoryException>(test);
        }

        [Fact]
        public async Task FallbackFunctionsCanReturnPlainTask()
        {
            int touched = 0;
            await Fallback.ExecuteAsync(
                () => { ++touched; return Task.CompletedTask; },
                () => { ++touched; return Task.CompletedTask; });
            Assert.Equal(1, touched);

            await Fallback.ExecuteAsync(
                () => { ++touched; throw new Exception(); },
                () => { ++touched; return Task.CompletedTask; });
            Assert.Equal(3, touched);
        }

        [Fact]
        public async Task FallbackSignalsIfPrimaryThrows()
        {
            int signaled = 0;
            await Fallback.ExecuteAsync(
                () => throw new Exception(),
                () => Task.CompletedTask,
                ex => ++signaled);
            Assert.Equal(1, signaled);
        }

        [Fact]
        public async Task FallbackSignalsIfSecondaryThrows()
        {
            int signaled = 0;

            try
            {
                await Fallback.ExecuteAsync(
                    () => throw new Exception(),
                    () => throw new Exception(),
                    ex => ++signaled);
            }
            catch
            {
                // ignored
            }

            Assert.Equal(2, signaled);
        }
    }
}
