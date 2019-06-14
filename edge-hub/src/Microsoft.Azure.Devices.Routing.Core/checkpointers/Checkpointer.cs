// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class Checkpointer : ICheckpointer
    {
        public static readonly DateTime DateTimeMinValue = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static readonly long InvalidOffset = -1L;

        readonly AtomicBoolean closed;
        readonly ICheckpointStore store;

        Checkpointer(string id, ICheckpointStore store, CheckpointData checkpointData)
        {
            this.Id = Preconditions.CheckNotNull(id);
            this.store = Preconditions.CheckNotNull(store);
            this.Offset = checkpointData.Offset;
            this.LastFailedRevivalTime = checkpointData.LastFailedRevivalTime;
            this.UnhealthySince = checkpointData.UnhealthySince;
            this.Proposed = checkpointData.Offset;
            this.closed = new AtomicBoolean(false);
        }

        public string Id { get; }

        public long Offset { get; private set; }

        public Option<DateTime> LastFailedRevivalTime { get; private set; }

        public Option<DateTime> UnhealthySince { get; private set; }

        public long Proposed { get; private set; }

        public bool HasOutstanding => this.Offset < this.Proposed;

        public static async Task<Checkpointer> CreateAsync(string id, ICheckpointStore store)
        {
            Preconditions.CheckNotNull(id);
            Preconditions.CheckNotNull(store);

            Events.CreateStart(id);
            CheckpointData checkpointData = await store.GetCheckpointDataAsync(id, CancellationToken.None);

            var checkpointer = new Checkpointer(id, store, checkpointData);

            Events.CreateFinished(checkpointer);
            return checkpointer;
        }

        public void Propose(IMessage message)
        {
            this.Proposed = Math.Max(message.Offset, this.Proposed);
        }

        public bool Admit(IMessage message)
        {
            return !this.closed && message.Offset > this.Offset;
        }

        public async Task CommitAsync(ICollection<IMessage> successful, ICollection<IMessage> remaining, Option<DateTime> lastFailedRevivalTime, Option<DateTime> unhealthySince, CancellationToken token)
        {
            Events.CommitStarted(this, successful.Count, remaining.Count);

            this.CheckClosed();

            long offset;
            if (remaining.Count == 0)
            {
                // Find the largest offset in the successful messages
                offset = successful.Aggregate(this.Offset, (acc, m) => Math.Max(acc, m.Offset));
            }
            else
            {
                // 1. Find the minimum offset in the remaining messages
                // 2. Find all of the successful messages with a smaller offset than the minimum offset from above
                // 3. Find the maximum offset in the filtered messages and use this as the checkpoint offset
                // This checkpoints up to but not including the minimum offset of the remaining messages
                // so that the remaining messages can be retried.
                long minOffsetRemaining = remaining.Min(m => m.Offset);
                offset = successful
                    .Where(m => m.Offset < minOffsetRemaining)
                    .Aggregate(this.Offset, (acc, m) => Math.Max(acc, m.Offset));
            }

            Debug.Assert(offset >= this.Offset);
            if (offset > this.Offset)
            {
                this.Offset = offset;
                this.LastFailedRevivalTime = lastFailedRevivalTime;
                this.UnhealthySince = unhealthySince;
                await this.store.SetCheckpointDataAsync(this.Id, new CheckpointData(offset, this.LastFailedRevivalTime, this.UnhealthySince), token);
            }

            Events.CommitFinished(this);
        }

        public async Task CloseAsync(CancellationToken token)
        {
            if (!this.closed.GetAndSet(true))
            {
                // Store the cached offset on closed to handle case where stores to the store are
                // throttled, but a clean shutdown is needed
                try
                {
                    if (this.Offset != InvalidOffset)
                    {
                        await this.store.SetCheckpointDataAsync(this.Id, new CheckpointData(this.Offset, this.LastFailedRevivalTime, this.UnhealthySince), token);
                        Events.Close(this);
                    }
                }
                catch (TaskCanceledException)
                {
                }
            }
        }

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
        }

        void CheckClosed()
        {
            if (this.closed)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Checkpointer \"{0}\" is closed.", this.Id));
            }
        }

        static class Events
        {
            const int IdStart = Routing.EventIds.Checkpointer;
            static readonly ILogger Log = Routing.LoggerFactory.CreateLogger<Checkpointer>();

            enum EventIds
            {
                CreateStart = IdStart,
                CreateFinished,
                CommitStarted,
                CommitFinished,
                Close,
            }

            public static void CreateStart(string id)
            {
                Log.LogInformation((int)EventIds.CreateStart, "[CheckpointerCreateStart] CheckpointerId: {id}", id);
            }

            public static void CreateFinished(Checkpointer checkpointer)
            {
                Log.LogInformation((int)EventIds.CreateFinished, "[CheckpointerCreateFinished] {context}", GetContextString(checkpointer));
            }

            public static void CommitStarted(Checkpointer checkpointer, int successfulCount, int remainingCount)
            {
                Log.LogInformation((int)EventIds.CommitStarted, "[CheckpointerCommitStarted] SuccessfulCount: {0}, RemainingCount: {1}, {2}", successfulCount, remainingCount, GetContextString(checkpointer));
            }

            public static void CommitFinished(Checkpointer checkpointer)
            {
                Log.LogInformation((int)EventIds.CommitFinished, "[CheckpointerCommitFinishedo] {context}", GetContextString(checkpointer));
            }

            public static void Close(Checkpointer checkpointer)
            {
                Log.LogInformation((int)EventIds.Close, "[CheckpointerClose] {conetxt}", GetContextString(checkpointer));
            }

            static string GetContextString(Checkpointer checkpointer)
            {
                return Invariant($"CheckpointerId: {checkpointer.Id}, Offset: {checkpointer.Offset}, Proposed: {checkpointer.Proposed}");
            }
        }
    }
}
