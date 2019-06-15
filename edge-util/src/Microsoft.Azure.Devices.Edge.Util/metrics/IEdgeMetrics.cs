// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    public interface IEdgeMetrics
    {
        void InitPrometheusMetrics(int port);
    }
}
