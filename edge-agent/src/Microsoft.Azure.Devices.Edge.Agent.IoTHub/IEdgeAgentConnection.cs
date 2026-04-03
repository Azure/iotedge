// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IEdgeAgentConnection : IDisposable
    {
        Option<PropertyCollection> ReportedProperties { get; }

        IModuleConnection ModuleConnection { get; }

        Task<Option<DeploymentConfigInfo>> GetDeploymentConfigInfoAsync();

        Task<bool> UpdateReportedPropertiesAsync(PropertyCollection patch);

        //// Task SendEventBatchAsync(IEnumerable<TelemetryMessage> messages);

        Task SendEventAsync(TelemetryMessage message);
    }
}
