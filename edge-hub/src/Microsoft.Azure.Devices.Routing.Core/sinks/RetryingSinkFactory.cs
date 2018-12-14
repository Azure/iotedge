// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Sinks
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class RetryingSinkFactory<T> : ISinkFactory<T>
    {
        readonly RetryPolicy retryPolicy;
        readonly ISinkFactory<T> underlying;

        public RetryingSinkFactory(ISinkFactory<T> underlying, RetryPolicy retryPolicy)
        {
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
            this.retryPolicy = Preconditions.CheckNotNull(retryPolicy, nameof(retryPolicy));
        }

        public async Task<ISink<T>> CreateAsync(string hubName)
        {
            ISink<T> sink = await this.underlying.CreateAsync(hubName);
            return new RetryingSink<T>(sink, this.retryPolicy);
        }
    }
}
