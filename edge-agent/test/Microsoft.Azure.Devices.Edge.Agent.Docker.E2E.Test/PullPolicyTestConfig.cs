// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.E2E.Test
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public class PullPolicyTestConfig
    {
        public PullPolicy PullPolicy { get; set; }

        public bool PullImage { get; set; }
    }
}
