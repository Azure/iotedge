// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class SuspendManagerTest
    {
        [Fact]
        public void TestCreate()
        {
            Assert.Throws<ArgumentNullException>(() => new SuspendManager(null));
            Assert.NotNull(new SuspendManager(SystemTime.Instance));
        }

        [Fact]
        public void IsNotSuspendedByDefault()
        {
            var manager = new SuspendManager(SystemTime.Instance);

            Assert.False(manager.IsSuspended());
        }

        [Fact]
        public async Task IsSuspendedWhenSuspended()
        {
            var cts = new CancellationTokenSource();
            var manager = new SuspendManager(SystemTime.Instance);

            await manager.SuspendUpdatesAsync(cts.Token);

            Assert.True(manager.IsSuspended());
        }

        [Fact]
        public async Task IsNotSuspendedWhenResumed()
        {
            var cts = new CancellationTokenSource();
            var manager = new SuspendManager(SystemTime.Instance);

            await manager.SuspendUpdatesAsync(cts.Token);
            await manager.ResumeUpdatesAsync(cts.Token);

            Assert.False(manager.IsSuspended());
        }

        [Fact]
        public async Task SuspendTimeout()
        {
            var systemTime = new Mock<ISystemTime>();
            var cts = new CancellationTokenSource();
            var manager = new SuspendManager(systemTime.Object);

            DateTime callTime = DateTime.UtcNow;
            systemTime.SetupGet(s => s.UtcNow)
                .Returns(() => callTime)
                .Callback(() => callTime = callTime.AddMinutes(10));

            await manager.SuspendUpdatesAsync(cts.Token);

            Assert.True(manager.IsSuspended());
            Assert.False(manager.IsSuspended());
        }

        [Fact]
        public async Task SuspendBlocksUntilCycleComplete()
        {
            var manager = new SuspendManager(SystemTime.Instance);

            using var cycle = await manager.BeginUpdateCycleAsync(CancellationToken.None);
            var suspendTask = manager.SuspendUpdatesAsync(CancellationToken.None);

            Assert.False(suspendTask.IsCompleted);
            Assert.False(manager.IsSuspended());

            cycle.Dispose();
            await suspendTask.TimeoutAfter(TimeSpan.FromSeconds(2));

            Assert.True(manager.IsSuspended());
        }
    }
}
