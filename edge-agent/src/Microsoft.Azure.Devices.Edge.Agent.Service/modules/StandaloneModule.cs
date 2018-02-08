// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using IModule = Autofac.Core.IModule;

    /// <summary>
    /// Autofac module for stand-alone mode.
    /// There are no external dependencies required, except Docker
    /// and a config file to define the desired modules.
    /// </summary>
    public class StandaloneModule : Module
    {
        readonly IModule agent;
        readonly IModule configSource;
        readonly IModule logging;

        public StandaloneModule(
            Uri dockerHost, string dockerLoggingDriver,
            IDictionary<string, string> dockerLoggingOptions, string configFilename,
            int maxRestartCount, TimeSpan intensiveCareTime, int coolOffTimeUnitInSeconds, IConfiguration configuration,
            EdgeHubConnectionString connectionDetails, string edgeDeviceConnectionString,
            bool usePersistentStorage, string storagePath)
        {
            this.agent = new AgentModule(Preconditions.CheckNotNull(dockerHost, nameof(dockerHost)), maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds, usePersistentStorage, storagePath);
            this.configSource = new FileConfigSourceModule(
                Preconditions.CheckNonWhiteSpace(configFilename, nameof(configFilename)),
                Preconditions.CheckNotNull(configuration, nameof(configuration)),
                Preconditions.CheckNotNull(connectionDetails, nameof(connectionDetails)),
                Preconditions.CheckNonWhiteSpace(edgeDeviceConnectionString, nameof(edgeDeviceConnectionString)));
            this.logging = new LoggingModule(
                Preconditions.CheckNonWhiteSpace(dockerLoggingDriver, nameof(dockerLoggingDriver)),
                Preconditions.CheckNotNull(dockerLoggingOptions, nameof(dockerLoggingOptions))
            );
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(this.agent);
            builder.RegisterModule(this.configSource);
            builder.RegisterModule(this.logging);
        }
    }
}
