// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class DockerModule : Module
    {
        readonly Uri dockerHostname;

        public DockerModule(Uri dockerHostname)
        {
            this.dockerHostname = Preconditions.CheckNotNull(dockerHostname, nameof(dockerHostname));
        }

        protected override void Load(ContainerBuilder builder)
        {

            // IDockerClient
            builder.Register(c => new DockerClientConfiguration(this.dockerHostname).CreateClient())
                .As<IDockerClient>()
                .SingleInstance();

            // ICommandFactory
            builder.Register(
                    async c =>
                    {
                        var dockerFactory = new DockerCommandFactory(
                                c.Resolve<IDockerClient>(),
                                c.Resolve<DockerLoggingConfig>(),
                                await c.Resolve<Task<IConfigSource>>());
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
