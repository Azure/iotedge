// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;

    using Microsoft.Azure.Devices.Common.ErrorHandling;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;

    public static class TestConstants
    {
        public const int DefaultRetryCount = 10;
        public const int DefaultBatchSize = 1000;
        public static readonly TimeSpan DefaultRevivePeriod = TimeSpan.FromMinutes(60);
        public static readonly TimeSpan DefaultMinBackoff = TimeSpan.FromSeconds(2);
        public static readonly TimeSpan DefaultMaxBackoff = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan DefaultDeltaBackoff = TimeSpan.FromSeconds(2);
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan DefaultBatchTimeout = TimeSpan.FromSeconds(1);

        public static ICheckpointer DefaultCheckpointer { get; } = NullCheckpointer.Instance;

        public static EndpointExecutorConfig DefaultConfig { get; } = new EndpointExecutorConfig(DefaultTimeout, DefaultRetryStrategy, DefaultRevivePeriod);

        public static AsyncEndpointExecutorOptions DefaultOptions { get; } = new AsyncEndpointExecutorOptions(DefaultBatchSize, DefaultBatchTimeout);

        public static RetryStrategy DefaultRetryStrategy { get; } = new ExponentialBackoffStrategy(DefaultRetryCount, DefaultMinBackoff, DefaultMaxBackoff, DefaultDeltaBackoff);
    }
}
