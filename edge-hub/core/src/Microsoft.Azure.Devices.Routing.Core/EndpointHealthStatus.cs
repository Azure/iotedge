// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    public enum EndpointHealthStatus
    {
        // Order determines coalescing behavior (higher wins).
        Unknown = 0,
        Healthy = 1,
        Unhealthy = 2,
        Dead = 3
    }
}
