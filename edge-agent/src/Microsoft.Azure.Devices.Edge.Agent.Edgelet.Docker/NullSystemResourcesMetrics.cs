// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Docker
{
    using System;
    using Microsoft.Extensions.Logging;

    public class NullSystemResourcesMetrics : ISystemResourcesMetrics
    {
        public void Start(ILogger logger)
        {
             logger.LogInformation("System Resource Metrics are NOT being collected");
        }
    }
}
