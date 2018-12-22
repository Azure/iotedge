// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public abstract class Source : IDisposable
    {
        public Router Router { get; }

        protected bool Disposed { get; private set; }

        protected Source(Router router)
        {
            this.Router = Preconditions.CheckNotNull(router);
        }

        public abstract Task RunAsync();

        public virtual Task CloseAsync(CancellationToken token) => this.Router.CloseAsync(token);

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.Disposed && disposing)
            {
                this.Router.Dispose();
            }
            this.Disposed = true;
        }
    }
}
