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
        public const int FileBackupConfigSource = EventIdStart + 500;
        public const int HealthRestartPlanner = EventIdStart + 600;
        public const int RestartManager = EventIdStart + 700;
        public const int IoTHubReporter = EventIdStart + 800;
        public const int DockerEnvironment = EventIdStart + 900;
        public const int ModuleLifecycleCommandFactory = EventIdStart + 1000;
        public const int DeviceClient = EventIdStart + 1100;

    }
}