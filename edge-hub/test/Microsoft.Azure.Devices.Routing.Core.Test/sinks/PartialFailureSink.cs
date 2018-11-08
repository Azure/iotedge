// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Sinks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class PartialFailureSink<T> : ISink<T>
    {
        public PartialFailureSink(Exception exception)
        {
            this.Exception = exception;
        }

        public Exception Exception { get; }

        public Task CloseAsync(CancellationToken token) => TaskEx.Done;

        public Task<ISinkResult<T>> ProcessAsync(T t, CancellationToken token) =>
            this.ProcessAsync(new[] { t }, token);

        public Task<ISinkResult<T>> ProcessAsync(ICollection<T> ts, CancellationToken token)
        {
            ISinkResult<T> result;
            if (ts.Count <= 1)
            {
                // Only fail if we have more than one message
                result = new SinkResult<T>(ts);
            }
            else
            {
                T[] successful = ts.Take(ts.Count / 2).ToArray();
                T[] failed = ts.Skip(ts.Count / 2).ToArray();
                result = new SinkResult<T>(successful, failed, new SendFailureDetails(FailureKind.InternalError, this.Exception));
            }

            return Task.FromResult(result);
        }
    }
}
