// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System;
    using System.Globalization;
    using System.Threading;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class AsyncEndpointExecutorOptions
    {
        public int BatchSize { get; }

        public TimeSpan BatchTimeout { get; }

        public AsyncEndpointExecutorOptions(int batchSize)
            : this(batchSize, Timeout.InfiniteTimeSpan)
        {
        }

        public AsyncEndpointExecutorOptions(int batchSize, TimeSpan batchTimeout)
        {
            Preconditions.CheckArgument(batchSize > 0, string.Format(CultureInfo.InvariantCulture, "Batch size must be greater than zero. Given {0}", batchSize));
            this.BatchSize = batchSize;
            this.BatchTimeout = batchTimeout;
        }
    }
}