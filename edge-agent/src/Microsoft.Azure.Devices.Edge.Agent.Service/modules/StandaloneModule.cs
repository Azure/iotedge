// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using Autofac;
    using Autofac.Core;
    using Microsoft.Azure.Devices.Edge.Util;

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

        public StandaloneModule(Uri dockerHost, string dockerLoggingDriver, string configFilename)
        {
            this.agent = new AgentModule(Preconditions.CheckNotNull(dockerHost, nameof(dockerHost)));
            this.configSource = new FileConfigSourceModule(Preconditions.CheckNonWhiteSpace(configFilename, nameof(configFilename)));
            this.logging = new LoggingModule(Preconditions.CheckNonWhiteSpace(dockerLoggingDriver, nameof(dockerLoggingDriver)));
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(this.agent);
            builder.RegisterModule(this.configSource);
            builder.RegisterModule(this.logging);
        }
    }
}