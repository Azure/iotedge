// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using Autofac;
    using Autofac.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

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
            int maxRestartCount, TimeSpan intensiveCareTime, int coolOffTimeUnitInSeconds, IConfiguration configuration)
        {
            this.agent = new AgentModule(Preconditions.CheckNotNull(dockerHost, nameof(dockerHost)), maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds);
            this.configSource = new FileConfigSourceModule(
                Preconditions.CheckNonWhiteSpace(configFilename, nameof(configFilename)),
                Preconditions.CheckNotNull(configuration, nameof(configuration))
            );
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