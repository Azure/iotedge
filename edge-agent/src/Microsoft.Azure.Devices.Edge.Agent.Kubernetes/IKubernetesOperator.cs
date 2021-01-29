// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Threading;

    public interface IKubernetesOperator : IDisposable
    {
        void Start(CancellationTokenSource shutdownCts);

        void Stop();
    }
}
