// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class AgentModule : Module
    {
        readonly Uri dockerHostname;

        public AgentModule(Uri dockerHostname)
        {
            this.dockerHostname = Preconditions.CheckNotNull(dockerHostname, nameof(dockerHostname));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IDockerClient
            builder.Register(c => new DockerClientConfiguration(this.dockerHostname).CreateClient())
                .As<IDockerClient>()
                .SingleInstance();

            // IEnvironment
            builder.Register(c => new DockerEnvironment(c.Resolve<IDockerClient>()))
                .As<IEnvironment>()
                .SingleInstance();

            // IPlanner
            builder.Register(c => new RestartPlanner(c.Resolve<ICommandFactory>()))
                .As<IPlanner>()
                .SingleInstance();

            // ICommandFactory
            builder.Register(
                c =>
                {
                    var docker = new DockerCommandFactory(c.Resolve<IDockerClient>());
                    return new LoggingCommandFactory(docker, c.Resolve<ILoggerFactory>());
                })
                .As<ICommandFactory>()
                .SingleInstance();

            // Task<Agent>
            builder.Register(
                async c => await Agent.CreateAsync(
                    await c.Resolve<Task<IConfigSource>>(),
                    c.Resolve<IEnvironment>(),
                    c.Resolve<IPlanner>())
                )
                .As<Task<Agent>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}