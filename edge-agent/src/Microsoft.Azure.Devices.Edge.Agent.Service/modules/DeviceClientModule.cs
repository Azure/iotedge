// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Util;

    class DeviceClientModule : Module
    {
        readonly EdgeHubConnectionString connectionDetails;
        readonly Option<UpstreamProtocol> upstreamProtocol;

        public DeviceClientModule(EdgeHubConnectionString connectionStringBuilder, Option<UpstreamProtocol> upstreamProtocol)
        {
            this.connectionDetails = Preconditions.CheckNotNull(connectionStringBuilder, nameof(connectionStringBuilder));
            this.upstreamProtocol = upstreamProtocol;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => DeviceClient.Create(this.connectionDetails, this.upstreamProtocol) as IDeviceClient)
                .As<IDeviceClient>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
