// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public struct AgentEventIds
    {
        public const int Agent = EventIdStart;
        public const int FileConfigSource = EventIdStart + 100;
        public const int TwinConfigSource = EventIdStart + 200;
        public const int DeploymentFileBackup = EventIdStart + 500;
        public const int HealthRestartPlanner = EventIdStart + 600;
        public const int RestartManager = EventIdStart + 700;
        public const int IoTHubReporter = EventIdStart + 800;
        public const int DockerEnvironment = EventIdStart + 900;
        public const int ModuleLifecycleCommandFactory = EventIdStart + 1000;
        public const int EdgeAgentConnection = EventIdStart + 1100;
        public const int ModuleClient = EventIdStart + 1200;
        public const int RetryingServiceClient = EventIdStart + 1300;
        public const int OrderedRetryPlanRunner = EventIdStart + 1400;
        public const int ModuleManagementHttpClient = EventIdStart + 1500;
        public const int ModuleIdentityLifecycleManager = EventIdStart + 1600;
        public const int RequestManager = EventIdStart + 1700;
        public const int AzureBlobLogsUploader = EventIdStart + 1800;
        public const int StreamRequestListener = EventIdStart + 1900;
        public const int LogsStreamRequestHandler = EventIdStart + 2000;
        public const int LogsProvider = EventIdStart + 2100;
        public const int LogsUploadRequestHandler = EventIdStart + 2200;
        public const int ModuleClientProvider = EventIdStart + 2300;
        public const int LogsRequestHandler = EventIdStart + 2400;
        public const int TaskStatusRequestHandler = EventIdStart + 2500;
        public const int RestartRequestHandler = EventIdStart + 2600;
        public const int BackupConfigSource = EventIdStart + 2700;
        const int EventIdStart = 100000;
    }
}
