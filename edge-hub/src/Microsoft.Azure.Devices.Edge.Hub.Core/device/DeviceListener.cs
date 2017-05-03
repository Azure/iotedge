// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;

    class DeviceListener : IDeviceListener
    {
        readonly IHubDeviceIdentity hubDeviceIdentity;
        readonly IRouter router;
        readonly IDispatcher dispatcher;
        readonly IConnectionManager connectionManager;
        readonly ICloudProxy cloudProxy;

        public DeviceListener(IHubDeviceIdentity hubDeviceIdentity, IRouter router, IDispatcher dispatcher, IConnectionManager connectionManager, ICloudProxy cloudProxy)
        {
            this.hubDeviceIdentity = hubDeviceIdentity;
            this.router = router;
            this.dispatcher = dispatcher;
            this.connectionManager = connectionManager;
            this.cloudProxy = cloudProxy;
        }

        public Task<object> CallMethod(string methodName, object parameters, string deviceId)
        {
            return this.dispatcher.CallMethod(methodName, parameters, deviceId);
        }

        public void BindDeviceProxy(IDeviceProxy deviceProxy)
        {
            ICloudListener cloudListener = new CloudListener(deviceProxy);
            this.cloudProxy.BindCloudListener(cloudListener);
            this.connectionManager.AddDeviceConnection(this.hubDeviceIdentity, deviceProxy);
        }

        public Task CloseAsync()
        {
            return this.connectionManager.CloseConnection(this.hubDeviceIdentity.Id);
        }

        public Task<Twin> GetTwin(string deviceId)
        {
            throw new NotImplementedException();
        }

        public Task ReceiveMessage(IMessage message)
        {
            return this.router.RouteMessage(message, this.hubDeviceIdentity.Id);
        }

        public Task ReceiveMessageBatch(IEnumerable<IMessage> messages) => this.router.RouteMessageBatch(messages, this.hubDeviceIdentity.Id);

        public Task UpdateReportedProperties(TwinCollection reportedProperties, string deviceId)
        {
            throw new NotImplementedException();
        }
    }
}
