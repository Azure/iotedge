// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.IO;
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
            Try<int> result = await Fallback.ExecuteAsync(
                () => Task.FromResult(1),
                () =>
                {
                    secondary = true;
                    return Task.FromResult(2);
                });
            Assert.True(result.Success);
            Assert.Equal(1, result.Value);
            Assert.False(secondary);
        }

        [Fact]
        public async Task FallbackInvokesSecondaryIfPrimaryThrows()
        {
            Try<int> result = await Fallback.ExecuteAsync(
                () => throw new Exception(),
                () => Task.FromResult(2));
            Assert.True(result.Success);
            Assert.Equal(2, result.Value);
        }

        [Fact]
        public async Task FallbackFailsIfPrimaryAndSecondaryThrow()
        {
            var exception1 = new Exception();
            var exception2 = new SecondaryException();
            Try<int> result = await Fallback.ExecuteAsync<int>(
                () => throw exception1,
                () => throw exception2);
            Assert.False(result.Success);
            Assert.IsType<AggregateException>(result.Exception);
            var aggregateException = result.Exception as AggregateException;
            Assert.NotNull(aggregateException);
            Assert.NotNull(aggregateException.InnerExceptions);
            Assert.Equal(2, aggregateException.InnerExceptions.Count);
            Assert.Contains(exception1, aggregateException.InnerExceptions);
            Assert.Contains(exception2, aggregateException.InnerExceptions);
        }

        [Fact]
        public async Task FallbackThrowsIfPrimaryThrowsFatalException()
        {
            await Assert.ThrowsAsync<OutOfMemoryException>(
                () => Fallback.ExecuteAsync(
                    () => throw new OutOfMemoryException(),
                    () => Task.FromResult(2)));
        }

        [Fact]
        public async Task FallbackFunctionsCanReturnPlainTask()
        {
            int touched = 0;
            await Fallback.ExecuteAsync(
                () =>
                {
                    ++touched;
                    return Task.CompletedTask;
                },
                () =>
                {
                    ++touched;
                    return Task.CompletedTask;
                });
            Assert.Equal(1, touched);

            await Fallback.ExecuteAsync(
                () =>
                {
                    ++touched;
                    throw new Exception();
                },
                () =>
                {
                    ++touched;
                    return Task.CompletedTask;
                });
            Assert.Equal(3, touched);

            Try<bool> result = await Fallback.ExecuteAsync(
                () => throw new Exception(),
                () => throw new Exception(),
                () => throw new Exception(),
                () => throw new Exception(),
                () =>
                {
                    ++touched;
                    return Task.CompletedTask;
                },
                () => throw new Exception(),
                () =>
                {
                    ++touched;
                    return Task.CompletedTask;
                });
            Assert.True(result.Success);
            Assert.True(result.Value);
            Assert.Equal(4, touched);
        }

        [Fact]
        public async Task FallbackAcceptsMultipleOptions()
        {
            Try<int> result = await Fallback.ExecuteAsync(
                () => throw new Exception(),
                () => throw new Exception(),
                () => throw new Exception(),
                () => throw new Exception(),
                () => throw new Exception(),
                () => throw new Exception(),
                () => Task.FromResult(2));
            Assert.True(result.Success);
            Assert.Equal(2, result.Value);
        }

        [Fact]
        public async Task FallbackFailsIfMultipleOptionsAllThrow()
        {
            var exception1 = new InvalidOperationException();
            var exception2 = new ArgumentException();
            var exception3 = new IOException();
            Try<bool> result = await Fallback.ExecuteAsync(
                () => throw exception1,
                () => throw exception2,
                () => throw exception3);
            Assert.False(result.Success);
            Assert.IsType<AggregateException>(result.Exception);
            var aggregateException = result.Exception as AggregateException;
            Assert.NotNull(aggregateException);
            Assert.NotNull(aggregateException.InnerExceptions);
            Assert.Equal(3, aggregateException.InnerExceptions.Count);
            Assert.Contains(exception1, aggregateException.InnerExceptions);
            Assert.Contains(exception2, aggregateException.InnerExceptions);
            Assert.Contains(exception3, aggregateException.InnerExceptions);
        }

        class SecondaryException : Exception
        {
        }
    }
}
