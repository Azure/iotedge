// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public struct AgentEventIds
    {
        const int EventIdStart = 100000;
        public const int Agent = EventIdStart;
        public const int FileConfigSource = EventIdStart + 100;
        public const int TwinConfigSource = EventIdStart + 200;
        public const int RestartPlanner = EventIdStart + 300;
        public const int Plan = EventIdStart + 400;
    }
}