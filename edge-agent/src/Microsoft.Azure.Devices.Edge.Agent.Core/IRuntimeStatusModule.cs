// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public interface IRuntimeStatusModule 
    {
        IModule WithRuntimeStatus(ModuleStatus newStatus);
    }
}
