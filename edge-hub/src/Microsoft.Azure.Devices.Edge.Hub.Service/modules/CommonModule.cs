// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
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
                    //IConnectionManager connectionManager = await c.Resolve<Task<IConnectionManager>>();
                    //ICredentialsStore credentialsStore = await c.Resolve<Task<ICredentialsStore>>();
                    //var tokenCredentialsAuthenticator = new TokenCacheAuthenticator(connectionManager, credentialsStore, this.iothubHostName);
                    var securityScopeCache = await c.Resolve<Task<ISecurityScopeEntitiesCache>>();
                    var securityScopeAuthenticator = new SecurityScopeTokenAuthenticator(securityScopeCache, this.iothubHostName, "EdgeHubHostName");
                    return new Authenticator(securityScopeAuthenticator, this.deviceId) as IAuthenticator;
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
