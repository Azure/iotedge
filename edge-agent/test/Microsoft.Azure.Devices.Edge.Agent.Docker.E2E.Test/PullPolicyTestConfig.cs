// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.E2E.Test
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Newtonsoft.Json;

    public class PullPolicyTestConfig
    {
        [JsonProperty("pullPolicy")]
        public PullPolicy PullPolicy { get; set; }

        [JsonProperty("pullImage")]
        public bool PullImage { get; set; }
    }
}
