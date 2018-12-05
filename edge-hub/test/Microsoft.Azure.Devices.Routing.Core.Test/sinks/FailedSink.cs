// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Test.Sinks
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class FailedSink<T> : ISink<T>
    {
        static readonly IList<T> Empty = new List<T>();

        public Exception Exception { get; }

        public FailedSink(Exception exception)
        {
            this.Exception = exception;
        }

        public Task<ISinkResult<T>> ProcessAsync(T t, CancellationToken token) =>
            this.ProcessAsync(new[] { t }, token);

        public Task<ISinkResult<T>> ProcessAsync(ICollection<T> ts, CancellationToken token)
        {
            ISinkResult<T> result = new SinkResult<T>(Empty, ts, new SendFailureDetails(FailureKind.InternalError, this.Exception));
            return Task.FromResult(result);
        }

        public Task CloseAsync(CancellationToken token) => TaskEx.Done;
    }
}
