// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class SuspendManager : ISuspendManager
    {
        static readonly TimeSpan maxSuspendPeriod = TimeSpan.FromMinutes(15);
        readonly ISystemTime systemTime;
        readonly AsyncLock reconcileLock = new AsyncLock();
        DateTime suspendTime = DateTime.MaxValue;

        public SuspendManager(ISystemTime systemTime)
        {
            this.systemTime = Preconditions.CheckNotNull(systemTime, nameof(systemTime));
        }

        public async Task<IDisposable> BeginUpdateCycleAsync(CancellationToken token)
            => await this.reconcileLock.LockAsync(token);

        public bool IsSuspended()
        {
            if (this.suspendTime == DateTime.MaxValue)
            {
                return false;
            }

            TimeSpan elapsedTime = this.systemTime.UtcNow - this.suspendTime;
            if (elapsedTime >= maxSuspendPeriod)
            {
                this.suspendTime = DateTime.MaxValue;
                return false;
            }

            Events.UpdatesSuspended(elapsedTime, maxSuspendPeriod - elapsedTime);
            return true;
        }

        public async Task SuspendUpdatesAsync(CancellationToken token)
        {
            // wait for update cycle to complete before returning
            using var cycle = await this.reconcileLock.LockAsync(token);

            // allow extending the suspend timeout each call
            this.suspendTime = this.systemTime.UtcNow;
        }

        public Task ResumeUpdatesAsync(CancellationToken token)
        {
            this.suspendTime = DateTime.MaxValue;

            // do not wait for update cycle
            return Task.CompletedTask;
        }

        static class Events
        {
            const int IdStart = AgentEventIds.RestartManager + 50;
            static readonly ILogger Log = Logger.Factory.CreateLogger<SuspendManager>();

            enum EventIds
            {
                UpdatesSuspended = IdStart + 1
            }

            public static void UpdatesSuspended(TimeSpan elapsedTime, TimeSpan remainingTime)
            {
                Log.LogInformation(
                    (int)EventIds.UpdatesSuspended,
                    $"Updates have been suspended for {elapsedTime.Humanize()} (auto-resume in {remainingTime.Humanize()}).");
            }
        }
    }
}
