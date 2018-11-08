// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

        public bool HasOutstanding => false;

        public string Id => "null";

        public Option<DateTime> LastFailedRevivalTime => Option.None<DateTime>();

        public long Offset => Checkpointer.InvalidOffset;

        public long Proposed => Checkpointer.InvalidOffset;

        public Option<DateTime> UnhealthySince => Option.None<DateTime>();

        public bool Admit(IMessage message) => true;

        public Task CloseAsync(CancellationToken token) => TaskEx.Done;

        public Task CommitAsync(ICollection<IMessage> successful, ICollection<IMessage> remaining, Option<DateTime> lastFailedRevivalTime, Option<DateTime> unhealthySince, CancellationToken token) => TaskEx.Done;

        public void Dispose() => this.Dispose(true);

        public void Propose(IMessage message)
        {
        }

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
