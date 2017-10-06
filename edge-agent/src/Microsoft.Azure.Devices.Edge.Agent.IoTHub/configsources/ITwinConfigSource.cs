// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public interface ITwinConfigSource : IConfigSource
    {
        ModuleSet ReportedModuleSet { get; }
    }
}
