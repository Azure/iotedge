// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public static class Constants
    {
        public const string OwnerValue = "Microsoft.Azure.Devices.Edge.Agent";

        // Connection string of the Edge Device.
        public const string EdgeDeviceConnectionStringKey = "EdgeDeviceConnectionString";

        // Connection string base for Edge Hub Modules
        public const string EdgeHubConnectionStringKey = "EdgeHubConnectionString";

        public const string IotHubConnectionStringKey = "IotHubConnectionString";

        public const string ModuleIdKey = "ModuleId";

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

        public const string EdgeModuleCaCertificateFileKey = "EdgeModuleCACertificateFile";

        public const string EdgeModuleHubServerCaChainCertificateFileKey = "EdgeModuleHubServerCAChainCertificateFile";

        public const string EdgeModuleHubServerCertificateFileKey = "EdgeModuleHubServerCertificateFile";

        public const string Unknown = "Unknown";

        public const string UpstreamProtocolKey = "UpstreamProtocol";

        public const string ModuleIdentityEdgeManagedByValue = "IotEdge";

        public const string EdgeletManagementUriVariableName = "IOTEDGE_MANAGEMENTURI";

        public const string EdgeletWorkloadUriVariableName = "IOTEDGE_WORKLOADURI";

        public const string IotHubHostnameVariableName = "IOTEDGE_IOTHUBHOSTNAME";

        public const string GatewayHostnameVariableName = "IOTEDGE_GATEWAYHOSTNAME";

        public const string DeviceIdVariableName = "IOTEDGE_DEVICEID";

        public const string ModuleIdVariableName = "IOTEDGE_MODULEID";

        public const string EdgeletAuthSchemeVariableName = "IOTEDGE_AUTHSCHEME";

        public const string EdgeletModuleGenerationIdVariableName = "IOTEDGE_MODULEGENERATIONID";

        public const string EdgeletApiVersionVariableName = "IOTEDGE_APIVERSION";

        public const string ModeKey = "Mode";

        public const string IotedgedMode = "iotedged";

        public const string DockerMode = "docker";

        public const string KubernetesMode = "kubernetes";

        public const string NetworkIdKey = "NetworkId";

        public const string EdgeletClientApiVersion = "2019-11-05";

        public const string EdgeletInitializationVectorFileName = "IOTEDGE_BACKUP_IV";

        public const string EnableStreams = "EnableStreams";

        public const string RequestTimeoutSecs = "RequestTimeoutSecs";

        public const string AllModulesIdentifier = "all";

        public const string CloseOnIdleTimeout = "CloseCloudConnectionOnIdleTimeout";

        public const string IdleTimeoutSecs = "CloudConnectionIdleTimeoutSecs";

        public const string IoTEdgeAgentProductInfoIdentifier = "EdgeAgent";

        public const string StorageMaxTotalWalSize = "RocksDB_MaxTotalWalSize";

        public const string WorkloadApiVersion = "2019-01-30";

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
