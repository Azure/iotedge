// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.Util.Concurrency;
    using AsyncLock = Microsoft.Azure.Devices.Routing.Core.Util.Concurrency.AsyncLock;

    public class MasterCheckpointer : ICheckpointer, ICheckpointerFactory
    {
        static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);
        static readonly ICollection<IMessage> Empty = ImmutableList<IMessage>.Empty;

        readonly AtomicBoolean closed;
        readonly ICheckpointStore store;
        readonly AtomicReference<ImmutableDictionary<string, ICheckpointer>> childCheckpointers;
        readonly AsyncLock sync;

        public string Id { get; }

        public long Offset { get; private set; }

        public Option<DateTime> LastFailedRevivalTime => Option.None<DateTime>();

        public Option<DateTime> UnhealthySince => Option.None<DateTime>();

        public long Proposed { get; }

        public bool HasOutstanding => false;

        ImmutableDictionary<string, ICheckpointer> ChildCheckpointers => this.childCheckpointers;

        MasterCheckpointer(string id, ICheckpointStore store, long offset)
        {
            this.Id = Preconditions.CheckNotNull(id);
            this.store = Preconditions.CheckNotNull(store);
            this.Offset = offset;
            this.Proposed = offset;
            this.closed = new AtomicBoolean(false);
            this.childCheckpointers = new AtomicReference<ImmutableDictionary<string, ICheckpointer>>(ImmutableDictionary<string, ICheckpointer>.Empty);
            this.sync = new AsyncLock();
        }

        public static async Task<MasterCheckpointer> CreateAsync(string id, ICheckpointStore store)
        {
            Preconditions.CheckNotNull(id);
            Preconditions.CheckNotNull(store);

            Events.CreateStart(id);

            CheckpointData checkpointData = await store.GetCheckpointDataAsync(id, CancellationToken.None);
            long offset = checkpointData.Offset;
            var masterCheckpointer = new MasterCheckpointer(id, store, offset);

            Events.CreateFinished(masterCheckpointer);

            return masterCheckpointer;
        }

        public async Task<ICheckpointer> CreateAsync(string id)
        {
            Events.CreateChildStart(this, id);

            Checkpointer checkpointer = await Checkpointer.CreateAsync(id, this.store);
            var child = new ChildCheckpointer(this, checkpointer);
            await this.AddChild(child);

            Events.CreateChildFinished(this, id);

            return child;
        }

        public void Propose(IMessage message)
        {
            throw new NotSupportedException();
        }

        public bool Admit(IMessage message)
        {
            Events.Admit(this, message.Offset, this.closed);

            return !this.closed && message.Offset > this.Offset;
        }

        public async Task CommitAsync(ICollection<IMessage> successful, ICollection<IMessage> remaining, Option<DateTime> lastFailedRevivalTime, Option<DateTime> unhealthySince, CancellationToken token)
        {
            using (await this.sync.LockAsync(token))
            {
                await this.CommitInternalAsync(successful, remaining, token);
            }
        }

        async Task CommitInternalAsync(ICollection<IMessage> successful, ICollection<IMessage> remaining, CancellationToken token)
        {
            long offset;

            if (this.ChildCheckpointers.Values.Any())
            {
                bool hasOutstanding = false;
                long outstanding = long.MaxValue;
                long completed = Checkpointer.InvalidOffset;

                Events.CommitStarted(this, successful.Count, remaining.Count);

                // 1. Calculate the minimum offset from all of the checkpointers with outstanding messages
                // 2. Calculate the maximum offset of all completed checkpointers.
                // 3. If there are outstanding checkpointers, checkpoint to the minimum of the outstanding
                //    If all of the checkpointers have completed, checkpoint to the maximum of the completed
                // 4. Ensure that the global checkpoint does not move backwards. This can occur if a disabled
                //    checkpoint is re-enabled with an old checkpoint and the router has checkpointed past it.
                foreach (ICheckpointer checkpointer in this.ChildCheckpointers.Values)
                {
                    if (checkpointer.HasOutstanding)
                    {
                        hasOutstanding = true;
                        outstanding = Math.Min(outstanding, checkpointer.Offset);
                    }
                    else
                    {
                        completed = Math.Max(completed, checkpointer.Offset);
                    }
                }

                offset = hasOutstanding ? outstanding : Math.Max(completed, this.OffsetFromMessages(successful, remaining));
                offset = Math.Max(offset, this.Offset);
            }
            else
            {
                // If there are no child checkpointers, then checkpoint based on the messages given.
                // This is to handle the degenerate case where a router is configured with no routes (all are disabled perhaps).
                // Messages still need to be checkpointed so that re-enabled endpoints don't see old messages.
                offset = this.OffsetFromMessages(successful, remaining);
            }

            Debug.Assert(offset >= this.Offset);
            if (offset > this.Offset)
            {
                this.Offset = offset;
                await this.store.SetCheckpointDataAsync(this.Id, new CheckpointData(this.Offset), token);
            }

            Events.CommitFinished(this);
        }

        long OffsetFromMessages(ICollection<IMessage> successful, ICollection<IMessage> remaining)
        {
            long offset;

            // If there are no outstanding children, checkpoint the messages given.
            // This can happen in two cases:
            //   1. One endpoint is being written to and we need to keep up with its checkpointer.
            //   2. No endpoints are being written to and we should advance the checkpoint.
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
            return offset;
        }

        public async Task CloseAsync(CancellationToken token)
        {
            if (!this.closed.GetAndSet(true))
            {
                // Store the cached offset on closed to handle case where stores to the store are
                // throttled, but a clean shutdown is needed
                try
                {
                    if (this.Offset != Checkpointer.InvalidOffset)
                    {
                        await this.store.SetCheckpointDataAsync(this.Id, new CheckpointData(this.Offset), CancellationToken.None);
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
            if (disposing)
            {
                this.sync.Dispose();
            }
        }

        async Task AddChild(ICheckpointer checkpointer)
        {
            using (await this.sync.LockAsync())
            {
                this.childCheckpointers.GetAndSet(this.ChildCheckpointers.SetItem(checkpointer.Id, checkpointer));
            }
        }

        async Task RemoveChild(string id)
        {
            using (await this.sync.LockAsync())
            {
                this.childCheckpointers.GetAndSet(this.ChildCheckpointers.Remove(id));
            }
        }

        class ChildCheckpointer : ICheckpointer
        {
            readonly MasterCheckpointer master;
            readonly ICheckpointer underlying;

            public ChildCheckpointer(MasterCheckpointer master, ICheckpointer underlying)
            {
                this.master = Preconditions.CheckNotNull(master);
                this.underlying = Preconditions.CheckNotNull(underlying);
            }

            public string Id => this.underlying.Id;

            public long Offset => this.underlying.Offset;

            public Option<DateTime> LastFailedRevivalTime => this.underlying.LastFailedRevivalTime;

            public Option<DateTime> UnhealthySince => this.underlying.UnhealthySince;

            public long Proposed => this.underlying.Proposed;

            public bool HasOutstanding => this.underlying.HasOutstanding;

            public async void Propose(IMessage message)
            {
                try
                {
                    using (var cts = new CancellationTokenSource(DefaultTimeout))
                    {
                        using (await this.master.sync.LockAsync(cts.Token))
                        {
                            this.underlying.Propose(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // This should never throw. However, a thrown exception in the async void will
                    // cause the process to crash.
                    Events.ProposeFailed(this.master, this.Id, ex);
                }
            }

            public bool Admit(IMessage message) => this.underlying.Admit(message);

            public async Task CommitAsync(ICollection<IMessage> messages, ICollection<IMessage> remaining, Option<DateTime> lastFailedRevivalTime, Option<DateTime> unhealthySince, CancellationToken token)
            {
                using (await this.master.sync.LockAsync(token))
                {
                    await this.underlying.CommitAsync(messages, remaining, lastFailedRevivalTime, unhealthySince, token);
                    await this.master.CommitInternalAsync(messages, Empty, token);
                }
            }

            public async Task CloseAsync(CancellationToken token)
            {
                await this.master.RemoveChild(this.underlying.Id);
                await this.underlying.CloseAsync(token);
            }

            public void Dispose() => this.underlying.Dispose();
        }

        static class Events
        {
            const string Source = nameof(MasterCheckpointer);
            //static readonly ILog Log = Routing.Log;

            public static void CreateStart(string id)
            {
                //Log.Informational("MasterCheckpointerCreateStart", Source,
                //    string.Format(CultureInfo.InvariantCulture, "MasterCheckpointerId: {0}", id));
            }

            public static void CreateFinished(MasterCheckpointer masterCheckpointer)
            {
                //Log.Informational("MasterCheckpointerCreateFinished", Source,
                //    GetContextString(masterCheckpointer));
            }

            public static void CreateChildStart(MasterCheckpointer masterCheckpointer, string id)
            {
                //Log.Informational("ChildCheckpointerCreateStart", Source,
                //    string.Format(CultureInfo.InvariantCulture, "ChildCheckpointerId: {0}, {1}", id, GetContextString(masterCheckpointer)));
            }

            public static void CreateChildFinished(MasterCheckpointer masterCheckpointer, string id)
            {
                //Log.Informational("ChildCheckpointerCreateFinished", Source,
                //    string.Format(CultureInfo.InvariantCulture, "ChildCheckpointerId: {0}, {1}", id, GetContextString(masterCheckpointer)));
            }

            public static void ProposeFailed(MasterCheckpointer masterCheckpointer, string id, Exception exception)
            {
                //Log.Error("ChildCheckpointerProposeFailed", Source,
                //    string.Format(CultureInfo.InvariantCulture, "ChildCheckpointId: {0}, {1}", id, GetContextString(masterCheckpointer)),
                //    exception);
            }

            public static void Admit(MasterCheckpointer masterCheckpointer, long messageOffset, bool isClosed)
            {
                //Log.Informational("MasterCheckpointerAdmit", Source,
                //    string.Format(CultureInfo.InvariantCulture, "MessageOffset: {0}, IsClosed: {1}, {2}", messageOffset, isClosed, GetContextString(masterCheckpointer)));
            }

            public static void CommitStarted(MasterCheckpointer masterCheckpointer, int successfulCount, int remainingCount)
            {
                //Log.Informational("MasterCheckpointerCommitStarted", Source,
                //    string.Format(CultureInfo.InvariantCulture, "SuccessfulCount: {0}, RemainingCount: {1}, {2}", successfulCount, remainingCount, GetContextString(masterCheckpointer)));
            }

            public static void CommitFinished(MasterCheckpointer masterCheckpointer)
            {
                //Log.Informational("MasterCheckpointerCommitFinished", Source,
                //    GetContextString(masterCheckpointer));
            }

            public static void Close(MasterCheckpointer masterCheckpointer)
            {
                //Log.Informational("MasterCheckpointerClose", Source,
                //    GetContextString(masterCheckpointer));
            }

            static string GetContextString(MasterCheckpointer masterCheckpointer)
            {
                return string.Format(CultureInfo.InvariantCulture, "MasterCheckpointerId: {0}, MasterCheckpointerOffset: {1}, ChildCheckpointersCount: {2}",
                    masterCheckpointer.Id, masterCheckpointer.Offset, masterCheckpointer.ChildCheckpointers.Count);
            }
        }
    }
}
