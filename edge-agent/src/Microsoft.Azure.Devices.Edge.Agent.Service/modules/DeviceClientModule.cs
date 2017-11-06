// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System.Threading.Tasks;
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
            // IDeviceClient
            builder.Register(
                    c =>
                    {
                        IDeviceClient dc = DeviceClient.Create(this.connectionDetails);
                        return dc;
                    })
                .As<IDeviceClient>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
