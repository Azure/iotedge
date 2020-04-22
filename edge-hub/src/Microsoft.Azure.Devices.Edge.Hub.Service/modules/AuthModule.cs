// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System.Threading.Tasks;

    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.AuthAgent;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    class AuthModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(
                        async c =>
                        {
                            var auth = await c.Resolve<Task<IAuthenticator>>();
                            var usernameParser = c.Resolve<IUsernameParser>();
                            var identityFactory = c.Resolve<IClientCredentialsFactory>();

                            return new AuthAgentListener(auth, usernameParser, identityFactory);
                        })
                    .As<Task<AuthAgentListener>>()
                    .SingleInstance();

            base.Load(builder);
        }
    }
}
