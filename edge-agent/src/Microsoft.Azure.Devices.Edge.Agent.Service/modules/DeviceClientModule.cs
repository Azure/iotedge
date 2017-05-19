// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Util;

    class DeviceClientModule : Module
    {
        readonly string connectionString;

        public DeviceClientModule(string connectionString)
        {
            this.connectionString = Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IDeviceClient
            builder.Register(c => new DeviceClient(this.connectionString))
                .As<IDeviceClient>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
