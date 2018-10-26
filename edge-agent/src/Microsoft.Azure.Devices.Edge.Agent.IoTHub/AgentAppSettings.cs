// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
 
    public class AgentAppSettings : IAgentAppSettings
    {
        const string EdgeAgentStorageFolder = "edgeAgent";
        const string VersionInfoFileName = "versionInfo.json";

        readonly AppSettings appSettings;

        public AgentAppSettings(string filePath)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(filePath)
                .AddEnvironmentVariables()
                .Build();

            this.appSettings = config.Get<AppSettings>();
            
            this.ConfigRefreshFrequency = TimeSpan.FromSeconds(Convert.ToInt32(this.appSettings.ConfigRefreshFrequencySecs ?? "3600"));
            this.DockerRegistryAuthConfigSection = config.GetSection("DockerRegistryAuth");
            this.StoragePath = this.GetStoragePath(this.appSettings.StorageFolder);
            this.VersionInfo = VersionInfo.Get(VersionInfoFileName);

            this.Validate();
        }

        public string ApiVersion => this.appSettings.IoTEdge_ApiVersion;

        public string BackupConfigFilePath => this.appSettings.BackupConfigFilePath;

        public TimeSpan ConfigRefreshFrequency { get; }

        public TimeSpan CoolOffTimeUnit => TimeSpan.FromSeconds(this.appSettings.CoolOffTimeUnitInSeconds);

        public ConfigSource ConfigSource => this.appSettings.ConfigSource;

        public string DeviceConnectionString => this.appSettings.DeviceConnectionString;

        public string DeviceId => this.appSettings.IoTEdge_DeviceId;

        public string DockerLoggingDriver => this.appSettings.DockerLoggingDriver;

        public IDictionary<string, string> DockerLoggingOptions => this.appSettings.DockerLoggingOptions ?? new Dictionary<string, string>();

        public IConfigurationSection DockerRegistryAuthConfigSection { get; }

        public string DockerUri => this.appSettings.DockerUri;

        public string EdgeDeviceHostName => this.appSettings.EdgeDeviceHostName;

        public string EdgeHostCACertificateFile => this.appSettings.EdgeHostCACertificateFile;

        public string EdgeHostHubServerCAChainCertificateFile => this.appSettings.EdgeHostHubServerCAChainCertificateFile;

        public string EdgeHostHubServerCertificateFile => this.appSettings.EdgeHostHubServerCertificateFile;

        public string EdgeHubVolumeName => this.appSettings.EdgeHubVolumeName ?? string.Empty;

        public string EdgeHubVolumePath => this.appSettings.EdgeHubVolumePath ?? string.Empty;

        public string EdgeModuleCACertificateFile => this.appSettings.EdgeModuleCACertificateFile ?? string.Empty;

        public string EdgeModuleHubServerCAChainCertificateFile => this.appSettings.EdgeModuleHubServerCAChainCertificateFile ?? string.Empty;

        public string EdgeModuleHubServerCertificateFile => this.appSettings.EdgeModuleHubServerCertificateFile ?? string.Empty;

        public string EdgeModuleVolumeName => this.appSettings.EdgeModuleVolumeName ?? string.Empty;

        public string EdgeModuleVolumePath => this.appSettings.EdgeModuleVolumePath ?? string.Empty;

        public TimeSpan IntensiveCareTime => TimeSpan.FromMinutes(this.appSettings.IntensiveCareTimeInMinutes);

        public string IoTHubHostName => this.appSettings.IoTEdge_IoTHubHostName;

        public string ManagementUri => this.appSettings.IoTEdge_ManagementUri;

        public int MaxRestartCount => this.appSettings.MaxRestartCount;

        public string ModuleGenerationId => this.appSettings.IoTEdge_ModuleGenerationId;

        public string ModuleId => this.appSettings.IoTEdge_ModuleId ?? Constants.EdgeAgentModuleIdentityName;

        public string NetworkId => this.appSettings.NetworkId;

        public string RuntimeLogLevel => this.appSettings.RuntimeLogLevel ?? "info";

        public EdgeRuntimeMode RuntimeMode => string.IsNullOrWhiteSpace(this.appSettings.Mode) ? EdgeRuntimeMode.Docker : (EdgeRuntimeMode) Enum.Parse(typeof(EdgeRuntimeMode), this.appSettings.Mode, true);

        public string StoragePath { get; }

        public Option<UpstreamProtocol> UpstreamProtocol => this.appSettings.UpstreamProtocol.ToUpstreamProtocol();

        public bool UsePersistentStorage => this.appSettings.UsePersistentStorage.Equals("false", StringComparison.OrdinalIgnoreCase) ? false : true;

        public VersionInfo VersionInfo { get; }

        public string WorkloadUri => this.appSettings.IoTEdge_WorkloadUri;
        
        string GetStoragePath(string baseStoragePath)
        {
            if (string.IsNullOrWhiteSpace(baseStoragePath) || !Directory.Exists(baseStoragePath))
            {
                baseStoragePath = Path.GetTempPath();
            }

            string storagePath = Path.Combine(baseStoragePath, EdgeAgentStorageFolder);
            if (!Directory.Exists(storagePath))
            {
                Directory.CreateDirectory(storagePath);
            }

            return storagePath;
        }

        void Validate()
        {
            if (this.RuntimeMode == EdgeRuntimeMode.Docker)
            {
                Preconditions.CheckNonWhiteSpace(this.DockerUri, nameof(this.DockerUri));
            }

            if (this.RuntimeMode == EdgeRuntimeMode.Iotedged)
            {
                Preconditions.CheckNonWhiteSpace(this.ManagementUri, nameof(this.ManagementUri));
                Preconditions.CheckNonWhiteSpace(this.WorkloadUri, nameof(this.WorkloadUri));
            }

            Preconditions.CheckNotNull(this.DockerLoggingDriver, nameof(this.DockerLoggingDriver));
            Preconditions.CheckRange(this.MaxRestartCount, 1);
            Preconditions.CheckNonWhiteSpace(this.StoragePath, nameof(this.StoragePath));
            Preconditions.CheckNotNull(this.VersionInfo, nameof(this.VersionInfo));
        }

        // All these property names should be case-insensitive matched to field name in AppSettings json file or name of environment variable.
        // Don't remove set method of these properties; it is used when configuration binding
        class AppSettings
        {
            public string BackupConfigFilePath { get; set; }

            public string ConfigRefreshFrequencySecs { get; set; }

            public ConfigSource ConfigSource { get; set; }

            public uint CoolOffTimeUnitInSeconds { get; set; }

            public string DeviceConnectionString { get; set; }

            public string DockerLoggingDriver { get; set; }

            public Dictionary<string, string> DockerLoggingOptions { get; set; }

            public string DockerUri { get; set; }

            public string EdgeDeviceHostName { get; set; }

            public string EdgeHostCACertificateFile { get; set; }

            public string EdgeHostHubServerCAChainCertificateFile { get; set; }

            public string EdgeHostHubServerCertificateFile { get; set; }

            public string EdgeHubVolumeName { get; set; }

            public string EdgeHubVolumePath { get; set; }

            public string EdgeModuleCACertificateFile { get; set; }

            public string EdgeModuleHubServerCAChainCertificateFile { get; set; }

            public string EdgeModuleHubServerCertificateFile { get; set; }

            public string EdgeModuleVolumeName { get; set; }

            public string EdgeModuleVolumePath { get; set; }

            public uint IntensiveCareTimeInMinutes { get; set; }

            public string IoTEdge_ApiVersion { get; set; }

            public string IoTEdge_DeviceId { get; set; }

            public string IoTEdge_IoTHubHostName { get; set; }

            public string IoTEdge_ManagementUri { get; set; }

            public string IoTEdge_ModuleGenerationId { get; set; }

            public string IoTEdge_ModuleId { get; set; }

            public string IoTEdge_WorkloadUri { get; set; }

            public int MaxRestartCount { get; set; }

            public string Mode { get; set; }

            public string NetworkId { get; set; }

            public string RuntimeLogLevel { get; set; }

            public string StorageFolder { get; set; }

            public string UpstreamProtocol { get; set; }

            public string UsePersistentStorage { get; set; }
        }
    }
}
