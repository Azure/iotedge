// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    [Unit]
    public class PeriodicTaskTest
    {
        [Fact]
        public async Task TestPeriodicTaskTest()
        {
            // Arrange
            int counter = 0;
            Func<Task> work = async () =>
            {
                counter++;
                await Task.Delay(TimeSpan.FromSeconds(10));
                if (counter % 3 == 0)
                {
                    throw new InvalidOperationException();
                }
            };

            TimeSpan frequency = TimeSpan.FromSeconds(15);
            TimeSpan startAfter = TimeSpan.FromSeconds(25);
            var logger = Mock.Of<ILogger>();

            // Act
            using (new PeriodicTask(work, frequency, startAfter, logger, "test op"))
            {
                // Assert
                await Task.Delay(TimeSpan.FromSeconds(20));
                Assert.Equal(0, counter);
                await Task.Delay(TimeSpan.FromSeconds(10));
                Assert.Equal(1, counter);
                for (int i = 0; i < 3; i++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(25));
                    Assert.Equal(2 + i, counter);
                }
            }
        }

        [Fact]
        public async Task TestPeriodicTaskWithCtsTest()
        {
            // Arrange
            int counter = 0;
            bool taskCancelled = false;
            Func<CancellationToken, Task> work = async cts =>
            {
                counter++;

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cts);
                }
                catch (TaskCanceledException)
                {
                    taskCancelled = true;
                    throw;
                }

                if (counter % 3 == 0)
                {
                    throw new InvalidOperationException();
                }
            };

            TimeSpan frequency = TimeSpan.FromSeconds(15);
            TimeSpan startAfter = TimeSpan.FromSeconds(25);
            var logger = Mock.Of<ILogger>();

            // Act
            using (new PeriodicTask(work, frequency, startAfter, logger, "test op"))
            {
                // Assert
                await Task.Delay(TimeSpan.FromSeconds(20));
                Assert.Equal(0, counter);
                await Task.Delay(TimeSpan.FromSeconds(10));
                Assert.Equal(1, counter);
                for (int i = 0; i < 3; i++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(25));
                    Assert.Equal(2 + i, counter);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(20));
            Assert.True(taskCancelled);
            Assert.Equal(4, counter);
        }
    }
}
