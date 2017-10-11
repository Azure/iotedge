// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Util;

    class ServiceClientModule : Module
    {
        readonly EdgeHubConnectionString connectionDetails;

        public ServiceClientModule(EdgeHubConnectionString connectionString)
        {
            this.connectionDetails = Preconditions.CheckNotNull(connectionString, nameof(connectionString));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IServiceClient
            builder.Register(c => new ServiceClient(this.connectionDetails))
                .As<IServiceClient>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
