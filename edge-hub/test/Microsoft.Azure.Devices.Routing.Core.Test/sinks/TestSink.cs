// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Sinks
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class TestSink<T> : ISink<T>
    {
        public TestSink()
        {
            this.Processed = new List<T>();
            this.IsClosed = false;
        }

        public bool IsClosed { get; private set; }

        public IList<T> Processed { get; }

        public Task CloseAsync(CancellationToken token)
        {
            this.IsClosed = true;
            return TaskEx.Done;
        }

        public Task<ISinkResult<T>> ProcessAsync(T t, CancellationToken ct)
        {
            this.Processed.Add(t);
            ISinkResult<T> result = new SinkResult<T>(new[] { t });
            return Task.FromResult(result);
        }

        public Task<ISinkResult<T>> ProcessAsync(ICollection<T> ts, CancellationToken ct)
        {
            foreach (T t in ts)
            {
                this.Processed.Add(t);
            }

            ISinkResult<T> result = new SinkResult<T>(ts);
            return Task.FromResult(result);
        }
    }
}
