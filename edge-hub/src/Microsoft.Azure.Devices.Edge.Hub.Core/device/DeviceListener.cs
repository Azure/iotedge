// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;

    class DeviceListener : IDeviceListener
    {
        readonly string deviceId;
        readonly IRouter router;
        readonly IDispatcher dispatcher;
        readonly ICloudProxy cloudProxy;

        public DeviceListener(string deviceId, IRouter router, IDispatcher dispatcher, ICloudProxy cloudProxy)
        {
            this.deviceId = deviceId;
            this.router = router;
            this.dispatcher = dispatcher;
            this.cloudProxy = cloudProxy;
        }

        public async Task<object> CallMethod(string methodName, object parameters, string deviceId)
        {
            return await this.dispatcher.CallMethod(methodName, parameters, deviceId);
        }

        public async Task<Twin> GetTwin(string deviceId)
        {
            return await this.cloudProxy.GetTwin();
        }        

        public async Task ReceiveMessage(IMessage message)
        {
            await this.router.RouteMessage(message);
        }

        public Task ReceiveMessageBatch(IEnumerable<IMessage> messages) => this.router.RouteMessageBatch(messages);

        public Task UpdateReportedProperties(TwinCollection reportedProperties, string deviceId)
        {
            throw new NotImplementedException();
        }
    }
}
