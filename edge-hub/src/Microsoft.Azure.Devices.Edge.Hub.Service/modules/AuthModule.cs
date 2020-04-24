// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System.Threading.Tasks;

    using Autofac;

    using Microsoft.Azure.Devices.Edge.Hub.AuthAgent;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    class AuthModule : Module
    {
        private static readonly string defaultAddress = "http://localhost:7120/authenticate/";

        private readonly IConfiguration config;

        public AuthModule(IConfiguration config)
        {
            this.config = Preconditions.CheckNotNull(config, nameof(config));
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(
                        async c =>
                        {
                            var auth = await c.Resolve<Task<IAuthenticator>>();
                            var usernameParser = c.Resolve<IUsernameParser>();
                            var identityFactory = c.Resolve<IClientCredentialsFactory>();

                            var listeningAddress = this.config.GetValue("address", defaultAddress);

                            return new AuthAgentListener(auth, usernameParser, identityFactory, listeningAddress);
                        })
                    .As<Task<AuthAgentListener>>()
                    .SingleInstance();

            base.Load(builder);
        }
    }
}
