// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Test.Sinks
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class FailedSinkFactory<T> : ISinkFactory<T>
    {
        readonly Exception exception;

        public FailedSinkFactory(Exception exception)
        {
            this.exception = Preconditions.CheckNotNull(exception, nameof(exception));
        }

        public Task<ISink<T>> CreateAsync(string hubName)
        {
            ISink<T> sink = new FailedSink<T>(this.exception);
            return Task.FromResult(sink);
        }
    }
}
