// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    public static class Constants
    {
        public const string VersionInfoFileName = "versionInfo.json";
        public const string ConfigFileName = "appsettings_hub.json";
        public const string TopicNameConversionSectionName = "mqttTopicNameConversion";
        public const string EdgeHubStorageFolder = "edgeHub";
        public const string SslCertPathEnvName = "SSL_CERTIFICATE_PATH";
        public const string SslCertEnvName = "SSL_CERTIFICATE_NAME";
        public const string IotHubConnectionStringVariableName = "IotHubConnectionString";
        public const string IotHubHostnameVariableName = "IOTEDGE_IOTHUBHOSTNAME";
        public const string DeviceIdVariableName = "IOTEDGE_DEVICEID";
        public const string ModuleIdVariableName = "IOTEDGE_MODULEID";
        public const string ModuleGenerationIdVariableName = "IOTEDGE_MODULEGENERATIONID";
        public const string WorkloadUriVariableName = "IOTEDGE_WORKLOADURI";
        public const string EdgeDeviceHostNameKey = "EDGEDEVICEHOSTNAME";
        public const string EdgeDeviceHostnameVariableName = "EdgeDeviceHostName";
        public const string WorkloadApiVersion = "2018-06-28";
        public const int CertificateValidityDays = 90;
        public const string InitializationVectorFileName = "EdgeHubIV";
        public const string EdgeHubServerCAChainCertificateFileKey = "EdgeModuleHubServerCAChainCertificateFile";
        public const string EdgeHubServerCertificateFileKey = "EdgeModuleHubServerCertificateFile";
    }
}
