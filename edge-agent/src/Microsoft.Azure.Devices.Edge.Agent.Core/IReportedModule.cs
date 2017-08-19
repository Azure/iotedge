// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Newtonsoft.Json;

    public interface IReportedModule : IModule
    {
        [JsonProperty(PropertyName = "exitCode")]
        int ExitCode { get; }

        [JsonProperty(PropertyName = "statusDescription")]
        string StatusDescription { get; }

        [JsonProperty(PropertyName = "lastStartTime")]
        string LastStartTime { get; }

        [JsonProperty(PropertyName = "lastExitTime")]
        string LastExitTime { get; }
    }

    public interface IReportedModule<TConfig> : IReportedModule, IModule<TConfig>
    {
    }
}
