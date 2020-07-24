// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;

    public interface IDeploymentMetrics
    {
        void ComputeAvailability(ModuleSet desired, ModuleSet current);
        void IndicateCleanShutdown();
        void ReportIotHubSync(bool successful);
        IDisposable ReportDeploymentTime();
    }
}
