//Copyright(c) Microsoft.All rights reserved.
//Microsoft would like to thank its contributors, a list
//of whom are at http://aka.ms/entlib-contributors

//Licensed under the Apache License, Version 2.0 (the "License"); you
//may not use this file except in compliance with the License. You may
//obtain a copy of the License at

//http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
//implied. See the License for the specific language governing permissions
//and limitations under the License.

using System;

namespace Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling
{
    /// <summary>
    /// A retry strategy with a specified number of retry attempts and an incremental time interval between retries.
    /// </summary>
    public class Incremental : RetryStrategy
    {
        private readonly int retryCount;

        private readonly TimeSpan initialInterval;

        private readonly TimeSpan increment;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling.Incremental" /> class. 
        /// </summary>
        public Incremental() : this(RetryStrategy.DefaultClientRetryCount, RetryStrategy.DefaultRetryInterval, RetryStrategy.DefaultRetryIncrement)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling.Incremental" /> class with the specified retry settings.
        /// </summary>
        /// <param name="retryCount">The number of retry attempts.</param>
        /// <param name="initialInterval">The initial interval that will apply for the first retry.</param>
        /// <param name="increment">The incremental time value that will be used to calculate the progressive delay between retries.</param>
        public Incremental(int retryCount, TimeSpan initialInterval, TimeSpan increment) : this(null, retryCount, initialInterval, increment)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling.Incremental" /> class with the specified name and retry settings.
        /// </summary>
        /// <param name="name">The retry strategy name.</param>
        /// <param name="retryCount">The number of retry attempts.</param>
        /// <param name="initialInterval">The initial interval that will apply for the first retry.</param>
        /// <param name="increment">The incremental time value that will be used to calculate the progressive delay between retries.</param>
        public Incremental(string name, int retryCount, TimeSpan initialInterval, TimeSpan increment) : this(name, retryCount, initialInterval, increment, RetryStrategy.DefaultFirstFastRetry)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling.Incremental" /> class with the specified number of retry attempts, time interval, retry strategy, and fast start option. 
        /// </summary>
        /// <param name="name">The retry strategy name.</param>
        /// <param name="retryCount">The number of retry attempts.</param>
        /// <param name="initialInterval">The initial interval that will apply for the first retry.</param>
        /// <param name="increment">The incremental time value that will be used to calculate the progressive delay between retries.</param>
        /// <param name="firstFastRetry">true to immediately retry in the first attempt; otherwise, false. The subsequent retries will remain subject to the configured retry interval.</param>
        public Incremental(string name, int retryCount, TimeSpan initialInterval, TimeSpan increment, bool firstFastRetry) : base(name, firstFastRetry)
        {
            Guard.ArgumentNotNegativeValue(retryCount, "retryCount");
            Guard.ArgumentNotNegativeValue(initialInterval.Ticks, "initialInterval");
            Guard.ArgumentNotNegativeValue(increment.Ticks, "increment");
            this.retryCount = retryCount;
            this.initialInterval = initialInterval;
            this.increment = increment;
        }

        /// <summary>
        /// Returns the corresponding ShouldRetry delegate.
        /// </summary>
        /// <returns>The ShouldRetry delegate.</returns>
        public override ShouldRetry GetShouldRetry()
        {
            return delegate (int currentRetryCount, Exception lastException, out TimeSpan retryInterval)
            {
                if (currentRetryCount < this.retryCount)
                {
                    retryInterval = TimeSpan.FromMilliseconds(this.initialInterval.TotalMilliseconds + this.increment.TotalMilliseconds * currentRetryCount);
                    return true;
                }
                retryInterval = TimeSpan.Zero;
                return false;
            };
        }
    }
}