// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;

    public interface IKubernetesOperator : IDisposable
    {
        void Start();

        void Stop();
    }
}
