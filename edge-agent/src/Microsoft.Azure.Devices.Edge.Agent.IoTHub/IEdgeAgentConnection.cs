// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;

    public interface IEdgeAgentConnection : IDisposable
    {
        Task<Option<DeploymentConfigInfo>> GetDeploymentConfigInfoAsync();

        Option<TwinCollection> ReportedProperties { get; }

        Task UpdateReportedPropertiesAsync(TwinCollection patch);
    }
}
