// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Checkpointers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    class LoggedCheckpointer : ICheckpointer
    {
        readonly ICheckpointer underlying;

        public LoggedCheckpointer(ICheckpointer underlying)
        {
            this.underlying = underlying;
            this.Processed = new List<IMessage>();
        }

        public bool HasOutstanding => this.underlying.HasOutstanding;

        public string Id => this.underlying.Id;

        public Option<DateTime> LastFailedRevivalTime => this.underlying.LastFailedRevivalTime;

        public long Offset => this.underlying.Offset;

        public IList<IMessage> Processed { get; }

        public long Proposed => this.underlying.Proposed;

        public Option<DateTime> UnhealthySince => this.underlying.UnhealthySince;

        public bool Admit(IMessage message) => this.underlying.Admit(message);

        public Task CloseAsync(CancellationToken token) => this.underlying.CloseAsync(token);

        public Task CommitAsync(ICollection<IMessage> successful, ICollection<IMessage> failed, Option<DateTime> lastFailedRevivalTime, Option<DateTime> unhealthySince, CancellationToken token)
        {
            foreach (IMessage message in successful)
            {
                this.Processed.Add(message);
            }

            return this.underlying.CommitAsync(successful, failed, lastFailedRevivalTime, unhealthySince, token);
        }

        public void Dispose() => this.Dispose(true);

        public void Propose(IMessage message) => this.underlying.Propose(message);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.underlying.Dispose();
            }
        }
    }
}
