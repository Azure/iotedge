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

namespace Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling
{
    /// <summary>
    /// Represents a retry strategy that determines the number of retry attempts and the interval between retries.
    /// </summary>
    public abstract class RetryStrategy
    {
        /// <summary>
        /// Represents the default number of retry attempts.
        /// </summary>
        public static readonly int DefaultClientRetryCount = 10;

        /// <summary>
        /// Represents the default amount of time used when calculating a random delta in the exponential delay between retries.
        /// </summary>
        public static readonly TimeSpan DefaultClientBackoff = TimeSpan.FromSeconds(10.0);

        /// <summary>
        /// Represents the default maximum amount of time used when calculating the exponential delay between retries.
        /// </summary>
        public static readonly TimeSpan DefaultMaxBackoff = TimeSpan.FromSeconds(30.0);

        /// <summary>
        /// Represents the default minimum amount of time used when calculating the exponential delay between retries.
        /// </summary>
        public static readonly TimeSpan DefaultMinBackoff = TimeSpan.FromSeconds(1.0);

        /// <summary>
        /// Represents the default interval between retries.
        /// </summary>
        public static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromSeconds(1.0);

        /// <summary>
        /// Represents the default time increment between retry attempts in the progressive delay policy.
        /// </summary>
        public static readonly TimeSpan DefaultRetryIncrement = TimeSpan.FromSeconds(1.0);

        /// <summary>
        /// Represents the default flag indicating whether the first retry attempt will be made immediately,
        /// whereas subsequent retries will remain subject to the retry interval.
        /// </summary>
        public static readonly bool DefaultFirstFastRetry = true;

        private static RetryStrategy noRetry = new FixedInterval(0, RetryStrategy.DefaultRetryInterval);

        private static RetryStrategy defaultFixed = new FixedInterval(RetryStrategy.DefaultClientRetryCount, RetryStrategy.DefaultRetryInterval);

        private static RetryStrategy defaultProgressive = new Incremental(RetryStrategy.DefaultClientRetryCount, RetryStrategy.DefaultRetryInterval, RetryStrategy.DefaultRetryIncrement);

        private static RetryStrategy defaultExponential = new ExponentialBackoff(RetryStrategy.DefaultClientRetryCount, RetryStrategy.DefaultMinBackoff, RetryStrategy.DefaultMaxBackoff, RetryStrategy.DefaultClientBackoff);

        /// <summary>
        /// Returns a default policy that performs no retries, but invokes the action only once.
        /// </summary>
        public static RetryStrategy NoRetry
        {
            get
            {
                return RetryStrategy.noRetry;
            }
        }

        /// <summary>
        /// Returns a default policy that implements a fixed retry interval configured with the <see cref="F:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryStrategy.DefaultClientRetryCount" /> and <see cref="F:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryStrategy.DefaultRetryInterval" /> parameters.
        /// The default retry policy treats all caught exceptions as transient errors.
        /// </summary>
        public static RetryStrategy DefaultFixed
        {
            get
            {
                return RetryStrategy.defaultFixed;
            }
        }

        /// <summary>
        /// Returns a default policy that implements a progressive retry interval configured with the <see cref="F:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryStrategy.DefaultClientRetryCount" />, <see cref="F:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryStrategy.DefaultRetryInterval" />, and <see cref="F:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryStrategy.DefaultRetryIncrement" /> parameters.
        /// The default retry policy treats all caught exceptions as transient errors.
        /// </summary>
        public static RetryStrategy DefaultProgressive
        {
            get
            {
                return RetryStrategy.defaultProgressive;
            }
        }

        /// <summary>
        /// Returns a default policy that implements a random exponential retry interval configured with the <see cref="F:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryStrategy.DefaultClientRetryCount" />, <see cref="F:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryStrategy.DefaultMinBackoff" />, <see cref="F:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryStrategy.DefaultMaxBackoff" />, and <see cref="F:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryStrategy.DefaultClientBackoff" /> parameters.
        /// The default retry policy treats all caught exceptions as transient errors.
        /// </summary>
        public static RetryStrategy DefaultExponential
        {
            get
            {
                return RetryStrategy.defaultExponential;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the first retry attempt will be made immediately,
        /// whereas subsequent retries will remain subject to the retry interval.
        /// </summary>
        public bool FastFirstRetry
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the name of the retry strategy.
        /// </summary>
        public string Name
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryStrategy" /> class. 
        /// </summary>
        /// <param name="name">The name of the retry strategy.</param>
        /// <param name="firstFastRetry">true to immediately retry in the first attempt; otherwise, false. The subsequent retries will remain subject to the configured retry interval.</param>
        protected RetryStrategy(string name, bool firstFastRetry)
        {
            this.Name = name;
            this.FastFirstRetry = firstFastRetry;
        }

        /// <summary>
        /// Returns the corresponding ShouldRetry delegate.
        /// </summary>
        /// <returns>The ShouldRetry delegate.</returns>
        public abstract ShouldRetry GetShouldRetry();
    }
}