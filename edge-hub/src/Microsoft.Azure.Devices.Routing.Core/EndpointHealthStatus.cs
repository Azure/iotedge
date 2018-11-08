// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
