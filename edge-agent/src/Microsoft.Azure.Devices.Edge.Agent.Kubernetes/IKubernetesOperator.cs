// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IKubernetesOperator : IDisposable
    {
        void Start();

        Task CloseAsync(CancellationToken token);
    }
}
