// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Sources
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

        public TestSource(Router router)
            : base(router)
        {
            this.closed = new AtomicBoolean(false);
            this.cts = new CancellationTokenSource();
        }

        public bool Closed => this.closed;

        public override async Task CloseAsync(CancellationToken token)
        {
            if (!this.closed.GetAndSet(true))
            {
                this.cts.Cancel();
                await base.CloseAsync(token);
            }
        }

        public override async Task RunAsync()
        {
            while (!this.cts.IsCancellationRequested)
            {
                await this.cts.Token.WhenCanceled();
            }
        }

        public Task SendAsync(IMessage[] messages) =>
            this.closed ? TaskEx.Done : this.Router.RouteAsync(messages);
    }
}
