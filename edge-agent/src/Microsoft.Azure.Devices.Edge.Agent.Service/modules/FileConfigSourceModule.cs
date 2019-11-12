// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Reporters;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public class FileConfigSourceModule : Module
    {
        readonly string configFilename;
        readonly IConfiguration configuration;

        public FileConfigSourceModule(
            string configFilename,
            IConfiguration configuration)
        {
            this.configFilename = Preconditions.CheckNonWhiteSpace(configFilename, nameof(configFilename));
            this.configuration = Preconditions.CheckNotNull(configuration, nameof(configuration));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // Task<IConfigSource>
            builder.Register(
                    async c =>
                    {
                        var serde = c.Resolve<ISerde<DeploymentConfigInfo>>();
                        IConfigSource config = await FileConfigSource.Create(
                            this.configFilename,
                            this.configuration,
                            serde);
                        return config;
                    })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            // IReporter
            // TODO: When using a file backed config source we need to figure out
            // how reporting will work.
            builder.Register(c => NullReporter.Instance as IReporter)
                .As<IReporter>()
                .SingleInstance();

            // IRequestManager
            builder.Register(
                    c => new RequestManager(Enumerable.Empty<IRequestHandler>(), TimeSpan.Zero) as IRequestManager)
                .As<IRequestManager>()
                .SingleInstance();

            // Task<IStreamRequestListener>
            builder.Register(c => Task.FromResult(new NullStreamRequestListener() as IStreamRequestListener))
                .As<Task<IStreamRequestListener>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
