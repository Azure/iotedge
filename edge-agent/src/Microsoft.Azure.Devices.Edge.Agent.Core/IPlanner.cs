// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    /// <summary>
    /// Allows the deployment strategy to be abstracted.
    /// </summary>
    public interface IPlanner
    {
        Plan Plan(ModuleSet desired, ModuleSet current);
    }
}