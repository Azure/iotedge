// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    // TODO: Should refactor further to group related parameters instead of a flat list
    public interface IAgentAppSettings
    {
        string ApiVersion { get; }

        string BackupConfigFilePath { get; }

        TimeSpan ConfigRefreshFrequency { get; }

        TimeSpan CoolOffTimeUnit { get; }

        ConfigSource ConfigSource { get; }

        string DeviceConnectionString { get; }

        string DeviceId { get; }

        string DockerLoggingDriver { get; }

        IDictionary<string, string> DockerLoggingOptions { get; }

        IConfigurationSection DockerRegistryAuthConfigSection { get; }

        IEnumerable<AuthConfig> DockerRegistryAuthConfigs { get; }

        string DockerUri { get; }

        string EdgeDeviceHostName { get; }

        string EdgeHostCACertificateFile { get; }

        string EdgeHostHubServerCAChainCertificateFile { get; }

        string EdgeHostHubServerCertificateFile { get; }

        string EdgeHubVolumeName { get; }

        string EdgeHubVolumePath { get; }

        string EdgeModuleCACertificateFile { get; }

        string EdgeModuleHubServerCAChainCertificateFile { get; }

        string EdgeModuleHubServerCertificateFile { get; }

        string EdgeModuleVolumeName { get; }

        string EdgeModuleVolumePath { get; }

        Option<IWebProxy> HttpsProxy { get; }

        TimeSpan IntensiveCareTime { get; }

        string IoTHubHostName { get; }

        ILogger Logger { get; }

        string ManagementUri { get; }

        int MaxRestartCount { get; }

        string ModuleGenerationId { get; }

        string ModuleId { get; }

        string NetworkId { get; }

        string ProductInfo { get; }

        string RuntimeLogLevel { get; }

        EdgeRuntimeMode RuntimeMode { get; }

        string StoragePath { get; }

        Option<UpstreamProtocol> UpstreamProtocol { get; }

        bool UsePersistentStorage { get; }

        VersionInfo VersionInfo { get; }

        string WorkloadUri { get; }
    }
}
