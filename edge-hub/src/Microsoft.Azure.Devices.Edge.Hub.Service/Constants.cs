// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Globalization;

    public static class Constants
    {
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
            public const string EdgeHubDevTrustBundleFile = "EdgeHubDevTrustBundleFile";
            public const string EdgeHubClientCertAuthEnabled = "ClientCertAuthEnabled";
        }

        public const int CertificateValidityDays = 90;
        public const string ConfigFileName = "appsettings_hub.json";
        public const string EdgeHubStorageFolder = "edgeHub";
        public const string InitializationVectorFileName = "EdgeHubIV";
        public const string TopicNameConversionSectionName = "mqttTopicNameConversion";
        public const string VersionInfoFileName = "versionInfo.json";
        public const string WorkloadApiVersion = "2018-06-28";
    }
}
