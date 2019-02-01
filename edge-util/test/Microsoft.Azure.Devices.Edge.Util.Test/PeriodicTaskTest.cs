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
            DateTime dt = DateTime.UtcNow;
            int counter = 0;
            Func<Task> work = async () =>
            {
                counter++;
                if (counter == 4)
                {
                    TimeSpan ts = DateTime.UtcNow - dt;
                }                

                await Task.Delay(TimeSpan.FromSeconds(2));
                if (counter % 3 == 0)
                {
                    throw new InvalidOperationException();
                }
            };

            TimeSpan frequency = TimeSpan.FromSeconds(3);
            TimeSpan startAfter = TimeSpan.FromSeconds(5);
            var logger = Mock.Of<ILogger>();

            // Act
            using (var periodicTask = new PeriodicTask(work, frequency, startAfter, logger, "test op"))
            {
                // Assert
                await Task.Delay(TimeSpan.FromSeconds(4));
                Assert.Equal(0, counter);
                await Task.Delay(TimeSpan.FromSeconds(2));
                Assert.Equal(1, counter);
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
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
                    await Task.Delay(TimeSpan.FromSeconds(3), cts);                    
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

            TimeSpan frequency = TimeSpan.FromSeconds(3);
            TimeSpan startAfter = TimeSpan.FromSeconds(5);
            var logger = Mock.Of<ILogger>();

            // Act
            using (var periodicTask = new PeriodicTask(work, frequency, startAfter, logger, "test op"))
            {
                // Assert
                await Task.Delay(TimeSpan.FromSeconds(4));
                Assert.Equal(0, counter);
                await Task.Delay(TimeSpan.FromSeconds(2));
                Assert.Equal(1, counter);
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(6));
                    Assert.Equal(2 + i, counter);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(4));
            Assert.True(taskCancelled);
        }
    }
}
