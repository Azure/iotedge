// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Util;

    class DeviceClientModule : Module
    {
        readonly EdgeHubConnectionString connectionDetails;

        public DeviceClientModule(EdgeHubConnectionString connectionStringBuilder)
        {
            this.connectionDetails = Preconditions.CheckNotNull(connectionStringBuilder, nameof(connectionStringBuilder));
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => DeviceClient.Create(this.connectionDetails) as IDeviceClient)
                .As<IDeviceClient>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
