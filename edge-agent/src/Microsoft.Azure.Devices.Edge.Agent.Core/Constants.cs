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

        public const string IotHubConnectionStringKey = "IotHubConnectionString";

        public const string ModuleIdKey = "ModuleId";

        public const string MMAStorePartitionKey = "mma";

        public const RestartPolicy DefaultRestartPolicy = RestartPolicy.OnUnhealthy;

        public const ModuleStatus DefaultDesiredStatus = ModuleStatus.Running;

        public const string EdgeHubModuleName = "edgeHub";

        public const string EdgeAgentModuleName = "edgeAgent";

        public const string EdgeHubModuleIdentityName = "$edgeHub";

        public const string EdgeAgentModuleIdentityName = "$edgeAgent";

        public const string EdgeDeviceHostNameKey = "EdgeDeviceHostName";

        public const string EdgeHubVolumeNameKey = "EdgeHubVolumeName";

        public const string EdgeModuleVolumeNameKey = "EdgeModuleVolumeName";

        public const string EdgeHubVolumePathKey = "EdgeHubVolumePath";

        public const string EdgeModuleVolumePathKey = "EdgeModuleVolumePath";

        public const string EdgeModuleCACertificateFileKey = "EdgeModuleCACertificateFile";

        public const string EdgeModuleHubServerCAChainCertificateFileKey = "EdgeModuleHubServerCAChainCertificateFile";

        public const string EdgeModuleHubServerCertificateFileKey = "EdgeModuleHubServerCertificateFile";

        public static class Labels
        {
            public const string Version = "net.azure-devices.edge.version";
            public const string Owner = "net.azure-devices.edge.owner";
            public const string RestartPolicy = "net.azure-devices.edge.restartPolicy";
            public const string DesiredStatus = "net.azure-devices.edge.desiredStatus";
            public const string NormalizedCreateOptions = "net.azure-devices.edge.normalizedCreateOptions";
            public const string ConfigurationId = "net.azure-devices.edge.configurationId";
        }
    }
}
