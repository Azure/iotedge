// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System.Threading.Tasks;

    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.AuthAgent;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    class AuthModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(
                        async c =>
                        {
                            var auth = await c.Resolve<Task<IAuthenticator>>();
                            return new AuthAgentListener(auth);
                        })
                    .As<Task<AuthAgentListener>>()
                    .SingleInstance();

            base.Load(builder);
        }
    }
}
