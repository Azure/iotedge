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
            // FIXME, probably should do other stuff
            this.IsActive = false;
            return Task.FromResult(true);
        }
    }
}
