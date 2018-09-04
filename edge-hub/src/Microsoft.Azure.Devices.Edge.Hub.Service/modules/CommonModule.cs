// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class CommonModule : Module
    {
        readonly string productInfo;
        readonly string iothubHostName;
        readonly string deviceId;

        public CommonModule(string productInfo, string iothubHostName, string deviceId)
        {
            this.productInfo = productInfo;
            this.iothubHostName = Preconditions.CheckNonWhiteSpace(iothubHostName, nameof(iothubHostName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // Task<IAuthenticator>
            builder.Register(async c =>
                {
                    var connectionManager = c.Resolve<IConnectionManager>();
                    ICredentialsStore credentialsStore = await c.Resolve<Task<ICredentialsStore>>();
                    var tokenCredentialsAuthenticator = new TokenCacheAuthenticator(new CloudTokenAuthenticator(connectionManager, this.iothubHostName), credentialsStore, this.iothubHostName);
                    return new Authenticator(tokenCredentialsAuthenticator, this.deviceId, credentialsStore) as IAuthenticator;
                })
                .As<Task<IAuthenticator>>()
                .SingleInstance();

            // IClientCredentialsFactory
            builder.Register(c => new ClientCredentialsFactory(this.iothubHostName, this.productInfo))
                .As<IClientCredentialsFactory>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
