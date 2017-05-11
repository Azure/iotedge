// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Test.sources
{
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.Util.Concurrency;

    [ExcludeFromCodeCoverage]
    public class TestSource : Source
    {
        readonly AtomicBoolean closed;
        readonly CancellationTokenSource cts;

        public bool Closed => this.closed;

        public TestSource(Router router)
            : base(router)
        {
            this.closed = new AtomicBoolean(false);
            this.cts = new CancellationTokenSource();
        }

        public Task SendAsync(IMessage[] messages) =>
            this.closed ? TaskEx.Done : this.Router.RouteAsync(messages);

        public override async Task RunAsync()
        {
            while (!this.cts.IsCancellationRequested)
            {
                await this.cts.Token.WhenCanceled();
            }
        }

        public override async Task CloseAsync(CancellationToken token)
        {
            if (!this.closed.GetAndSet(true))
            {
                this.cts.Cancel();
                await base.CloseAsync(token);
            }
        }
    }
}