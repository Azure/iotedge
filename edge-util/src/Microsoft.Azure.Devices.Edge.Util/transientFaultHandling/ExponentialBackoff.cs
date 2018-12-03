// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling
{
    /// <summary>
    /// A retry strategy with back-off parameters for calculating the exponential delay between retries.
    /// Note: this fixes an overflow in the stock ExponentialBackoff in the Transient Fault Handling library
    /// which causes the calculated delay to go negative.
    /// Use of this class for exponential backoff is encouraged instead.
    /// </summary>
    public class ExponentialBackoff : RetryStrategy
    {
        readonly int retryCount;
        readonly TimeSpan minBackoff;
        readonly TimeSpan maxBackoff;
        readonly TimeSpan deltaBackoff;

        public ExponentialBackoff()
            : this(DefaultClientRetryCount, DefaultMinBackoff, DefaultMaxBackoff, DefaultClientBackoff)
        {
        }

        public ExponentialBackoff(int retryCount, TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff)
            : this(retryCount, minBackoff, maxBackoff, deltaBackoff, DefaultFirstFastRetry)
        {
        }

        public ExponentialBackoff(int retryCount, TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff, bool firstFastRetry)
            : base(firstFastRetry)
        {
            Guard.ArgumentNotNegativeValue(retryCount, "retryCount");
            Guard.ArgumentNotNegativeValue(minBackoff.Ticks, "minBackoff");
            Guard.ArgumentNotNegativeValue(maxBackoff.Ticks, "minBackoff");
            Guard.ArgumentNotNegativeValue(deltaBackoff.Ticks, "deltaBackoff");
            Guard.ArgumentNotGreaterThan(minBackoff.TotalMilliseconds, maxBackoff.TotalMilliseconds, "minBackoff must be less than or equal to maxBackoff");
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
