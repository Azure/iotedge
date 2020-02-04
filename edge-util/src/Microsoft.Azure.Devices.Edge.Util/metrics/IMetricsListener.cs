// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using Microsoft.Extensions.Logging;

    public interface IMetricsListener : IDisposable
    {
        void Start(ILogger logger);
    }
}
