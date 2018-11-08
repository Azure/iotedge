// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    public static class Constants
    {
        public const int CertificateValidityDays = 90;
        public const string ConfigFileName = "appsettings_hub.json";
        public const string EdgeHubStorageFolder = "edgeHub";
        public const string InitializationVectorFileName = "EdgeHubIV";
        public const string TopicNameConversionSectionName = "mqttTopicNameConversion";
        public const string VersionInfoFileName = "versionInfo.json";
        public const string WorkloadApiVersion = "2018-06-28";

        public static class ConfigKey
        {
            public const string DeviceId = "IOTEDGE_DEVICEID";
            public const string EdgeDeviceHostName = "EDGEDEVICEHOSTNAME";
            public const string EdgeHubServerCAChainCertificateFile = "EdgeModuleHubServerCAChainCertificateFile";
            public const string EdgeHubServerCertificateFile = "EdgeModuleHubServerCertificateFile";
            public const string IotHubConnectionString = "IotHubConnectionString";
            public const string IotHubHostname = "IOTEDGE_IOTHUBHOSTNAME";
            public const string ModuleGenerationId = "IOTEDGE_MODULEGENERATIONID";
            public const string ModuleId = "IOTEDGE_MODULEID";
            public const string WorkloadUri = "IOTEDGE_WORKLOADURI";
            public const string EdgeHubDevServerCertificateFile = "EdgeHubDevServerCertificateFile";
            public const string EdgeHubDevServerPrivateKeyFile = "EdgeHubDevServerPrivateKeyFile";
        }
    }
}
