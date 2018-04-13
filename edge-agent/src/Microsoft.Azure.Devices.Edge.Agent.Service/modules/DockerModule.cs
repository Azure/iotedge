// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet.Models;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class DockerModule : Module
    {
        readonly Uri dockerHostname;
        readonly IEnumerable<AuthConfig> dockerAuthConfig;

        public DockerModule(Uri dockerHostname, IEnumerable<AuthConfig> dockerAuthConfig)
        {
            this.dockerHostname = Preconditions.CheckNotNull(dockerHostname, nameof(dockerHostname));
            this.dockerAuthConfig = Preconditions.CheckNotNull(dockerAuthConfig, nameof(dockerAuthConfig));
        }

        protected override void Load(ContainerBuilder builder)
        {

            // IDockerClient
            builder.Register(c => new DockerClientConfiguration(this.dockerHostname).CreateClient())
                .As<IDockerClient>()
                .SingleInstance();

            // ICombinedConfigProvider<CombinedDockerConfig>
            builder.Register(c => new CombinedDockerConfigProvider(this.dockerAuthConfig))
                .As<ICombinedConfigProvider<CombinedDockerConfig>>()
                .SingleInstance();

            // ICommandFactory
            builder.Register(
                    async c =>
                    {
                        var dockerClient = c.Resolve<IDockerClient>();
                        var dockerLoggingConfig = c.Resolve<DockerLoggingConfig>();
                        var combinedDockerConfigProvider = c.Resolve<ICombinedConfigProvider<CombinedDockerConfig>>();
                        IConfigSource configSource = await c.Resolve<Task<IConfigSource>>();
                        var dockerFactory = new DockerCommandFactory(dockerClient, dockerLoggingConfig, configSource, combinedDockerConfigProvider);
                        return new LoggingCommandFactory(dockerFactory, c.Resolve<ILoggerFactory>()) as ICommandFactory;
                    })
                .As<Task<ICommandFactory>>()
                .SingleInstance();

            // IEnvironment
            builder.Register(
                async c =>
                {
                    IEnvironment dockerEnvironment = await DockerEnvironment.CreateAsync(
                        c.Resolve<IDockerClient>(),
                        c.Resolve<IEntityStore<string, ModuleState>>(),
                        c.Resolve<IRestartPolicyManager>());
                    return dockerEnvironment;
                })
             .As<Task<IEnvironment>>()
             .SingleInstance();
        }
    }
}
