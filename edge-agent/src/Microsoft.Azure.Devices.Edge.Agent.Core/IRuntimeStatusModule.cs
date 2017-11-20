// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Newtonsoft.Json;

    public interface IRuntimeStatusModule 
    {
        IModule WithRuntimeStatus(ModuleStatus newStatus);
    }
}
