// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Util;

    class ServiceClientModule : Module
    {
        readonly EdgeHubConnectionString connectionDetails;
        readonly string edgeDeviceConnectionString;

        public ServiceClientModule(EdgeHubConnectionString connectionDetails, string edgeDeviceConnectionString)
        {
            this.connectionDetails = Preconditions.CheckNotNull(connectionDetails, nameof(connectionDetails));
            this.edgeDeviceConnectionString = Preconditions.CheckNonWhiteSpace(edgeDeviceConnectionString, nameof(edgeDeviceConnectionString));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IServiceClient
            builder.Register(c => new ServiceClient(this.edgeDeviceConnectionString, this.connectionDetails.DeviceId))
                .As<IServiceClient>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
