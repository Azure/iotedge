// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Checkpointers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Xunit;

    class LoggedCheckpointer : ICheckpointer
    {
        readonly ICheckpointer underlying;

        public IList<IMessage> Processed { get; }

        public LoggedCheckpointer(ICheckpointer underlying)
        {
            this.underlying = underlying;
            this.Processed = new List<IMessage>();
        }

        public string Id => this.underlying.Id;

        public long Offset => this.underlying.Offset;

        public Option<DateTime> LastFailedRevivalTime => this.underlying.LastFailedRevivalTime;

        public Option<DateTime> UnhealthySince => this.underlying.UnhealthySince;

        public long Proposed => this.underlying.Proposed;

        public bool HasOutstanding => this.underlying.HasOutstanding;

        public void Propose(IMessage message) => this.underlying.Propose(message);

        public bool Admit(IMessage message) => this.underlying.Admit(message);

        public Task CommitAsync(ICollection<IMessage> successful, ICollection<IMessage> failed, Option<DateTime> lastFailedRevivalTime, Option<DateTime> unhealthySince, CancellationToken token)
        {
            foreach (IMessage message in successful)
            {
                this.Processed.Add(message);
            }
            return this.underlying.CommitAsync(successful, failed, lastFailedRevivalTime, unhealthySince, token);
        }

        public Task CloseAsync(CancellationToken token) => this.underlying.CloseAsync(token);

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.underlying.Dispose();
            }
        }
    }

    [ExcludeFromCodeCoverage]
    public class LoggedCheckpointTest : RoutingUnitTestBase
    {
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] {1, 2, 3, 4}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} }, 1L);
        static readonly IMessage Message2 = new Message(TelemetryMessageSource.Instance, new byte[] {2, 3, 4, 1}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} }, 2L);
        static readonly IMessage Message3 = new Message(TelemetryMessageSource.Instance, new byte[] {3, 4, 1, 2}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} }, 3L);
        static readonly IMessage Message4 = new Message(TelemetryMessageSource.Instance, new byte[] {4, 1, 2, 3}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} }, 4L);

        [Fact, Unit]
        public async Task SmokeTest()
        {
            using (var checkpointer = new LoggedCheckpointer(new NullCheckpointer()))
            {
                Assert.Equal(Checkpointer.InvalidOffset, checkpointer.Offset);
                Assert.True(checkpointer.Admit(Message1));
                await checkpointer.CommitAsync(new[] { Message3, Message4 }, new IMessage[] {}, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);
                await checkpointer.CommitAsync(new[] { Message1, Message2 }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);

                Assert.Equal(new List<IMessage> { Message3, Message4, Message1, Message2 }, checkpointer.Processed);
                Assert.Equal(Checkpointer.InvalidOffset, checkpointer.Offset);
                await checkpointer.CloseAsync(CancellationToken.None);
            }
        }
    }
}
