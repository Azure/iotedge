// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;

    public interface IMetricsListener : IDisposable
    {
        void Start();
    }
}
