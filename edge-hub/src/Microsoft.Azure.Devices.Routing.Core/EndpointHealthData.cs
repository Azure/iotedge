// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core
{
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public sealed class EndpointHealthData
    {
        public string EndpointId { get; }

        public EndpointHealthStatus HealthStatus { get; }

        public EndpointHealthData(string endpointId, EndpointHealthStatus healthStatus)
        {
            this.EndpointId = Preconditions.CheckNotNull(endpointId);
            this.HealthStatus = healthStatus;
        }
    }
}
