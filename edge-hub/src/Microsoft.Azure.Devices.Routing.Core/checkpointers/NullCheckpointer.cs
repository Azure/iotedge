// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    /// <summary>
    /// Checkpointer that admits all messages and ignores commits.
    /// </summary>
    public class NullCheckpointer : ICheckpointer
    {
        public static ICheckpointer Instance { get; } = new NullCheckpointer();

        public string Id => "null";

        public long Offset => Checkpointer.InvalidOffset;

        public Option<DateTime> LastFailedRevivalTime => Option.None<DateTime>();

        public Option<DateTime> UnhealthySince => Option.None<DateTime>();

        public long Proposed => Checkpointer.InvalidOffset;

        public bool HasOutstanding => false;

        public void Propose(IMessage message)
        {
        }

        public bool Admit(IMessage message) => true;

        public Task CommitAsync(ICollection<IMessage> successful, ICollection<IMessage> remaining, Option<DateTime> lastFailedRevivalTime, Option<DateTime> unhealthySince, CancellationToken token) => TaskEx.Done;

        public Task CloseAsync(CancellationToken token) => TaskEx.Done;

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}