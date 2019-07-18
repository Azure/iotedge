// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Sinks
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NullSink<T> : ISink<T>
    {
        public Task<ISinkResult<T>> ProcessAsync(T t, CancellationToken token) =>
            this.ProcessAsync(new[] { t }, token);

        public Task<ISinkResult<T>> ProcessAsync(ICollection<T> ts, CancellationToken token)
        {
            ISinkResult<T> result = new SinkResult<T>(ts);
            return Task.FromResult(result);
        }

        public Task CloseAsync(CancellationToken token) => TaskEx.Done;
    }
}