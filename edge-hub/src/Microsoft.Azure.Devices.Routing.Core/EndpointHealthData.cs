// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public sealed class EndpointHealthData
    {
        public EndpointHealthData(string endpointId, EndpointHealthStatus healthStatus)
        {
            this.EndpointId = Preconditions.CheckNotNull(endpointId);
            this.HealthStatus = healthStatus;
        }

        public string EndpointId { get; }

        public EndpointHealthStatus HealthStatus { get; }
    }
}
