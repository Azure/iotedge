// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

        public FailedSink(Exception exception)
        {
            this.Exception = exception;
        }

        public Exception Exception { get; }

        public Task CloseAsync(CancellationToken token) => TaskEx.Done;

        public Task<ISinkResult<T>> ProcessAsync(T t, CancellationToken token) =>
            this.ProcessAsync(new[] { t }, token);

        public Task<ISinkResult<T>> ProcessAsync(ICollection<T> ts, CancellationToken token)
        {
            ISinkResult<T> result = new SinkResult<T>(Empty, ts, new SendFailureDetails(FailureKind.InternalError, this.Exception));
            return Task.FromResult(result);
        }
    }
}
