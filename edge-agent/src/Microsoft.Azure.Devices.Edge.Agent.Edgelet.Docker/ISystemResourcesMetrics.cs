// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Docker
{
    using System;
    using Microsoft.Extensions.Logging;

    public interface ISystemResourcesMetrics
    {
        void Start(ILogger logger);
    }
}
