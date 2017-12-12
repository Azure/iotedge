// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using Autofac;
    using Autofac.Core;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Autofac module for iothubConnected mode.
    /// There are no external dependencies required, except Docker
    /// and a connection string to connect to the twin.
    /// </summary>
    public class IotHubConnectedModule : Module
    {
        readonly IModule agent;
        readonly IModule configSource;
        readonly IModule logging;

        public IotHubConnectedModule(
            Uri dockerHost, string dockerLoggingDriver,
            IDictionary<string, string> dockerLoggingOptions,
            EdgeHubConnectionString connectionDetails,
            string edgeDeviceConnectionString,
            string backupConfigFilePath,
            int maxRestartCount,
            TimeSpan intensiveCareTime,
            int coolOffTimeUnitInSeconds,
            IConfiguration configuration,
            bool usePersistentStorage,
            string storagePath,
            VersionInfo versionInfo
        )
        {
            this.agent = new AgentModule(Preconditions.CheckNotNull(dockerHost, nameof(dockerHost)), maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds, usePersistentStorage, storagePath);
            this.configSource = new FileBackupConfigSourceModule(
                Preconditions.CheckNotNull(connectionDetails, nameof(connectionDetails)),
                Preconditions.CheckNonWhiteSpace(edgeDeviceConnectionString, nameof(edgeDeviceConnectionString)),
                Preconditions.CheckNonWhiteSpace(backupConfigFilePath, nameof(backupConfigFilePath)),
                Preconditions.CheckNotNull(configuration, nameof(configuration)),
                Preconditions.CheckNotNull(versionInfo, nameof(versionInfo))
            );
            this.logging = new LoggingModule(Preconditions.CheckNonWhiteSpace(dockerLoggingDriver, nameof(dockerLoggingDriver)), Preconditions.CheckNotNull(dockerLoggingOptions, nameof(dockerLoggingOptions)));
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(this.agent);
            builder.RegisterModule(this.configSource);
            builder.RegisterModule(this.logging);
        }
    }
}
