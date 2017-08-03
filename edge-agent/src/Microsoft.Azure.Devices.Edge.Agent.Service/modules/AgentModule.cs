// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;

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
            builder.Register(async c => new RestartPlanner(await c.Resolve<Task<ICommandFactory>>()) as IPlanner)
                .As<Task<IPlanner>>()
                .SingleInstance();

			// Task<Agent>
			builder.Register(
				async c => new Agent(
					await c.Resolve<Task<IConfigSource>>(),
					c.Resolve<IEnvironment>(),
					await c.Resolve<Task<IPlanner>>())
				)
				.As<Task<Agent>>()
				.SingleInstance();

			base.Load(builder);
        }
    }
}