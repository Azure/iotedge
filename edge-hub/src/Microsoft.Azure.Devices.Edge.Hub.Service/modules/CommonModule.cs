// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
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
            // IAuthenticator
            builder.Register(c =>
                {
                    
                    var tokenCredentialsAuthenticator = new TokenCredentialsAuthenticator(c.Resolve<IConnectionManager>(), c.Resolve<ICredentialsStore>(), this.iothubHostName);
                    return new Authenticator(tokenCredentialsAuthenticator, this.deviceId);
                })
                .As<IAuthenticator>()
                .SingleInstance();

            // IClientCredentialsFactory
            builder.Register(c => new ClientCredentialsFactory(this.iothubHostName, this.productInfo))
                .As<IClientCredentialsFactory>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
