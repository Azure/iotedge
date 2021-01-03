// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;

    public class BrokeredCloudConnection : ICloudConnection
    {
        public BrokeredCloudConnection(BrokeredCloudProxy cloudProxy)
        {
            this.IsActive = true;
            this.CloudProxy = Option.Some(cloudProxy as ICloudProxy);
        }

        public Option<ICloudProxy> CloudProxy { get; }

        public bool IsActive { get; private set; }

        public Task<bool> CloseAsync()
        {
            // TODO, in order to tear down the connection higher level,
            // it should unsubscribe from all topics
            this.IsActive = false;
            return Task.FromResult(true);
        }
    }
}
