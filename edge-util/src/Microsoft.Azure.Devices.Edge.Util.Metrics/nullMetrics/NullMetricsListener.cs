// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    using Microsoft.Extensions.Logging;

    public class NullMetricsListener : IMetricsListener
    {
        public void Dispose()
        {
        }

        public void Start(ILogger logger)
        {
        }
    }
}
