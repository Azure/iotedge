// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using Autofac;
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
            builder.Register(c => new Authenticator(c.Resolve<IConnectionManager>(), this.deviceId))
                .As<IAuthenticator>()
                .SingleInstance();

            // IIdentityFactory
            builder.Register(c => new IdentityFactory(this.iothubHostName, this.productInfo))
                .As<IIdentityFactory>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
