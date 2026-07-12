// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System;

    // Decouples a transport connectivity-recovery event from endpoint FSM retries.
    public interface IEndpointExecutorRetrySignal
    {
        event EventHandler RetryRequested;
    }

    public sealed class EndpointExecutorRetrySignal : IEndpointExecutorRetrySignal
    {
        public event EventHandler RetryRequested;

        public void RequestRetry() => this.RetryRequested?.Invoke(this, EventArgs.Empty);
    }
}
