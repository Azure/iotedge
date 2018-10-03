// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ResettableTimerTest
    {
        [Fact]
        public async Task CreateAndStartTimerTest()
        {
            // Arrange
            int callbackCalledCount = 0;
            Task Callback()
            {
                Interlocked.Increment(ref callbackCalledCount);
                return Task.CompletedTask;
            }

            TimeSpan period = TimeSpan.FromSeconds(3);
            var resettableTimer = new ResettableTimer(Callback, period, null);

            // Act
            resettableTimer.Start();
            await Task.Delay(TimeSpan.FromSeconds(7));

            // Assert
            Assert.Equal(2, callbackCalledCount);
        }

        [Fact]
        public async Task ResetTimerTest()
        {
            // Arrange
            int callbackCalledCount = 0;
            Task Callback()
            {
                Interlocked.Increment(ref callbackCalledCount);
                return Task.CompletedTask;
            }

            TimeSpan period = TimeSpan.FromSeconds(3);
            var resettableTimer = new ResettableTimer(Callback, period, null);

            // Act
            resettableTimer.Start();
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                resettableTimer.Reset();
            }

            await Task.Delay(TimeSpan.FromSeconds(4));

            // Assert
            Assert.Equal(1, callbackCalledCount);
        }

        [Fact]
        public async Task DisableEnableTimerTest()
        {
            // Arrange
            int callbackCalledCount = 0;
            Task Callback()
            {
                Interlocked.Increment(ref callbackCalledCount);
                return Task.CompletedTask;
            }

            TimeSpan period = TimeSpan.FromSeconds(3);
            var resettableTimer = new ResettableTimer(Callback, period, null);

            // Act            
            resettableTimer.Start();
            await Task.Delay(TimeSpan.FromSeconds(4));

            // Assert
            Assert.Equal(1, callbackCalledCount);

            // Act
            resettableTimer.Disable();
            await Task.Delay(TimeSpan.FromSeconds(4));

            // Assert
            Assert.Equal(1, callbackCalledCount);

            // Act
            resettableTimer.Enable();
            resettableTimer.Start();
            await Task.Delay(TimeSpan.FromSeconds(4));

            // Assert
            Assert.Equal(2, callbackCalledCount);

        }
    }
}
