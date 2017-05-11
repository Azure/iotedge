// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public sealed class EndpointHealthData
    {
        public string EndpointId { get; private set; }
        
        public EndpointHealthStatus HealthStatus { get; private set; }

        public EndpointHealthData(string endpointId, EndpointHealthStatus healthStatus)
        {
            this.EndpointId = Preconditions.CheckNotNull(endpointId);
            this.HealthStatus = healthStatus;
        }
    }
}
