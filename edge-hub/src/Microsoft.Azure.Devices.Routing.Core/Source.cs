// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    public abstract class Source : IDisposable
    {
        protected Source(Router router)
        {
            this.Router = Preconditions.CheckNotNull(router);
        }

        public Router Router { get; }

        protected bool Disposed { get; private set; }

        public virtual Task CloseAsync(CancellationToken token) => this.Router.CloseAsync(token);

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public abstract Task RunAsync();

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
