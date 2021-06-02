// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System.Threading.Tasks;

    using Autofac;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    class AuthModule : Module
    {
        static readonly int defaultPort = 7120;
        static readonly string defaultBaseUrl = "/authenticate/";

        readonly IConfiguration config;

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
                            var metadataStore = await c.Resolve<Task<IMetadataStore>>();
                            var usernameParser = c.Resolve<IUsernameParser>();
                            var identityFactory = c.Resolve<IClientCredentialsFactory>();
                            var systemIdProvider = c.Resolve<ISystemComponentIdProvider>();

                            var port = this.config.GetValue("port", defaultPort);
                            var baseUrl = this.config.GetValue("baseUrl", defaultBaseUrl);

                            var config = new AuthAgentProtocolHeadConfig(port, baseUrl);

                            return new AuthAgentProtocolHead(auth, metadataStore, usernameParser, identityFactory, systemIdProvider, config);
                        })
                    .As<Task<AuthAgentProtocolHead>>()
                    .SingleInstance();

            base.Load(builder);
        }
    }
}
