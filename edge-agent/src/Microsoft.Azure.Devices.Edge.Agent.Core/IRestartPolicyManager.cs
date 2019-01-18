// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;

    public interface IRestartPolicyManager
    {
        ModuleStatus ComputeModuleStatusFromRestartPolicy(ModuleStatus status, RestartPolicy restartPolicy, int restartCount, DateTime lastExitTime);

        IEnumerable<IRuntimeModule> ApplyRestartPolicy(IEnumerable<IRuntimeModule> modules);
    }
}
