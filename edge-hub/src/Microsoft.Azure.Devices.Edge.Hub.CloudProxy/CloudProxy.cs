// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using System;
    using System.Threading.Tasks;

    class CloudProxy : ICloudProxy
    {
        readonly DeviceClient deviceClient;

        public CloudProxy(DeviceClient deviceClient)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
        }

        public Task<bool> Disconnect()
        {
            throw new NotImplementedException();
        }

        public Task<Twin> GetTwin()
        {
            throw new NotImplementedException();
        }

        public Task SendFeedback(string lockToken, FeedbackStatus status)
        {
            throw new NotImplementedException();
        }

        public Task SendMessage(IMessage message)
        {
            throw new NotImplementedException();
        }

        public Task UpdateReportedProperties(TwinCollection reportedProperties)
        {
            throw new NotImplementedException();
        }
    }
}
