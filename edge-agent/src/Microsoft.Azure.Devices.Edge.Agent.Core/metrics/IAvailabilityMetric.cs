// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    public interface IAvailabilityMetric
    {
        void ComputeAvailability(ModuleSet desired, ModuleSet current);
        void IndicateCleanShutdown();
    }
}
