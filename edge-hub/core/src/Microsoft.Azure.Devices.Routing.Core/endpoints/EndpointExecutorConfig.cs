// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;

    public class EndpointExecutorConfig
    {
        public EndpointExecutorConfig(TimeSpan timeout, RetryStrategy retryStrategy, TimeSpan revivePeriod)
            : this(timeout, retryStrategy, revivePeriod, false)
        {
        }

        /// <summary>
        /// Configures runtime parameters of the async endpoint executor state machine
        /// </summary>
        /// <param name="timeout">Timeout for sending messages to the external endpoint and for checkpointing</param>
        /// <param name="retryStrategy">Timeout for sending messages to the external endpoint and for checkpointing</param>
        /// <param name="revivePeriod">Time spent in the Dead state before attempting to send messages to an endpoint again</param>
        /// <param name="throwOnDead">Should complete with exception when dead instead of transitioning to dead state</param>
        public EndpointExecutorConfig(TimeSpan timeout, RetryStrategy retryStrategy, TimeSpan revivePeriod, bool throwOnDead)
        {
            this.Timeout = timeout;
            this.RetryStrategy = retryStrategy;
            this.RevivePeriod = revivePeriod;
            this.ThrowOnDead = throwOnDead;
        }

        public TimeSpan Timeout { get; }

        public TimeSpan RevivePeriod { get; }

        public RetryStrategy RetryStrategy { get; }

        public bool ThrowOnDead { get; }
    }
}
