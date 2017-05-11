// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;
    using Microsoft.Azure.Devices.Common.ErrorHandling;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

    public static class TestConstants
    {
        public static readonly TimeSpan DefaultRevivePeriod = TimeSpan.FromMinutes(60);
        public static readonly TimeSpan DefaultMinBackoff = TimeSpan.FromSeconds(2);
        public static readonly TimeSpan DefaultMaxBackoff = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan DefaultDeltaBackoff = TimeSpan.FromSeconds(2);
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan DefaultBatchTimeout = TimeSpan.FromSeconds(1);
        public const int DefaultRetryCount = 10;

        public static RetryStrategy DefaultRetryStrategy { get; } = new ExponentialBackoffStrategy(DefaultRetryCount, DefaultMinBackoff, DefaultMaxBackoff, DefaultDeltaBackoff);
        public static EndpointExecutorConfig DefaultConfig { get; } = new EndpointExecutorConfig(DefaultTimeout, DefaultRetryStrategy, DefaultRevivePeriod);
        public static AsyncEndpointExecutorOptions DefaultOptions { get; } = new AsyncEndpointExecutorOptions(DefaultBatchSize, DefaultBatchTimeout);
        public static ICheckpointer DefaultCheckpointer { get; } = NullCheckpointer.Instance;
        public const int DefaultBatchSize = 1000;
    }
}
