// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

// ReSharper disable once CheckNamespace
namespace Microsoft.Azure.Devices.Common.ErrorHandling
{
    using System;
    using System.Diagnostics.Contracts;
    using Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling;

    /// <summary>
    /// A retry strategy with back-off parameters for calculating the exponential delay between retries.
    /// Note: this fixes an overflow in the stock ExponentialBackoff in the Transient Fault Handling library
    /// which causes the calculated delay to go negative.
    /// Use of this class for exponential backoff is encouraged instead.
    /// </summary>
    public class ExponentialBackoffStrategy : RetryStrategy
    {
        readonly int retryCount;
        readonly TimeSpan minBackoff;
        readonly TimeSpan maxBackoff;
        readonly TimeSpan deltaBackoff;

        public ExponentialBackoffStrategy()
            : this(DefaultClientRetryCount, DefaultMinBackoff, DefaultMaxBackoff, DefaultClientBackoff)
        {
        }

        public ExponentialBackoffStrategy(int retryCount, TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff)
            : this(null, retryCount, minBackoff, maxBackoff, deltaBackoff, DefaultFirstFastRetry)
        {
        }

        public ExponentialBackoffStrategy(string name, int retryCount, TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff)
            : this(name, retryCount, minBackoff, maxBackoff, deltaBackoff, DefaultFirstFastRetry)
        {
        }

        public ExponentialBackoffStrategy(string name, int retryCount, TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff, bool firstFastRetry)
            : base(name, firstFastRetry)
        {
            Contract.Assert(retryCount >= 0, "retryCount");
            Contract.Assert(minBackoff.Ticks >= 0, "minBackoff");
            Contract.Assert(maxBackoff.Ticks >= 0, "minBackoff");
            Contract.Assert(deltaBackoff.Ticks >= 0, "deltaBackoff");
            Contract.Assert(minBackoff.TotalMilliseconds <= maxBackoff.TotalMilliseconds, "minBackoff must be less than or equal to maxBackoff");
            this.retryCount = retryCount;
            this.minBackoff = minBackoff;
            this.maxBackoff = maxBackoff;
            this.deltaBackoff = deltaBackoff;
        }

        public override ShouldRetry GetShouldRetry()
        {
            return (int currentRetryCount, Exception lastException, out TimeSpan retryInterval) =>
            {
                if (currentRetryCount < this.retryCount)
                {
                    var random = new Random();
                    double length = Math.Min(
                        this.minBackoff.TotalMilliseconds + (Math.Pow(2.0, currentRetryCount) - 1.0) * (0.8 + random.NextDouble() * 0.4) * this.deltaBackoff.TotalMilliseconds,
                        this.maxBackoff.TotalMilliseconds);
                    retryInterval = TimeSpan.FromMilliseconds(length);
                    return true;
                }
                else
                {
                    retryInterval = TimeSpan.Zero;
                    return false;
                }
            };
        }
    }
}