// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Newtonsoft.Json;

    public class ModuleState
    {
        public int RestartCount { get; }
        public DateTime LastRestartTimeUtc { get; }

        [JsonConstructor]
        public ModuleState(int restartCount, DateTime lastRestartTimeUtc)
        {
            this.RestartCount = restartCount;
            this.LastRestartTimeUtc = lastRestartTimeUtc;
        }
    }
}
