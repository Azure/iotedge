// Copyright (c) Microsoft. All rights reserved.
using System;

namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub
{
    /// <summary>
    /// Contains information that is required for the <see cref="E:Microsoft.Azure.WebJobs.Extensions.EdgeHub.RetryPolicy.Retrying" /> event.
    /// </summary>
    class RetryingEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the current retry count.
        /// </summary>
        public int CurrentRetryCount
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the exception that caused the retry conditions to occur.
        /// </summary>
        public Exception LastException
        {
            get;
            set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.WebJobs.Extensions.EdgeHub.RetryingEventArgs" /> class.
        /// </summary>
        /// <param name="currentRetryCount">The current retry attempt count.</param>
        /// <param name="lastException">The exception that caused the retry conditions to occur.</param>
        public RetryingEventArgs(int currentRetryCount, Exception lastException)
        {
            Guard.ArgumentNotNull(lastException, "lastException");
            this.CurrentRetryCount = currentRetryCount;
            this.LastException = lastException;
        }
    }
}
