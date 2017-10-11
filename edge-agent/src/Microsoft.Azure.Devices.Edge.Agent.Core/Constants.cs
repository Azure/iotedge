// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public static class Constants
    {
        public const string Owner = "Microsoft.Azure.Devices.Edge.Agent";

        // Connection string of the Edge Device.
        public const string EdgeDeviceConnectionStringKey = "EdgeDeviceConnectionString";

        // Connection string base for Edge Hub Modules
        public const string EdgeHubConnectionStringKey = "EdgeHubConnectionString";

        public const string ModuleIdKey = "ModuleId";

        public const string MMAStorePartitionKey = "mma";

        public const RestartPolicy DefaultRestartPolicy = RestartPolicy.OnUnhealthy;

        public const ModuleStatus DefaultDesiredStatus = ModuleStatus.Running;

        public static class Labels
        {
            public const string Version = "net.azure-devices.edge.version";
            public const string Owner = "net.azure-devices.edge.owner";
            public const string RestartPolicy = "net.azure-devices.edge.restartPolicy";
            public const string DesiredStatus = "net.azure-devices.edge.desiredStatus";
            public const string NormalizedCreateOptions = "net.azure-devices.edge.normalizedCreateOptions";
        }
    }
}
