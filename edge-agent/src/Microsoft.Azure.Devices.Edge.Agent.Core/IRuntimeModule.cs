// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Newtonsoft.Json;

    public interface IRuntimeModule : IModule, IRuntimeStatusModule
    {
        [JsonProperty(PropertyName = "exitCode")]
        int ExitCode { get; }

        [JsonProperty(PropertyName = "statusDescription")]
        string StatusDescription { get; }

        [JsonProperty(PropertyName = "lastStartTimeUtc")]
        DateTime LastStartTimeUtc { get; }

        [JsonProperty(PropertyName = "lastExitTimeUtc")]
        DateTime LastExitTimeUtc { get; }

        [JsonProperty(PropertyName = "restartCount")]
        int RestartCount { get; }

        [JsonProperty(PropertyName = "lastRestartTimeUtc")]
        DateTime LastRestartTimeUtc { get; }

        [JsonProperty(PropertyName = "runtimeStatus")]
        ModuleStatus RuntimeStatus { get; }
    }

    public interface IRuntimeModule<TConfig> : IRuntimeModule, IModule<TConfig>
    {
    }
}
