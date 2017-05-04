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
        const string ModuleIdPropertyName = "module-Id";
        readonly IIdentity identity;
        readonly IRouter router;
        readonly IDispatcher dispatcher;
        readonly IConnectionManager connectionManager;
        readonly ICloudProxy cloudProxy;

        public DeviceListener(IIdentity identity, IRouter router, IDispatcher dispatcher, IConnectionManager connectionManager, ICloudProxy cloudProxy)
        {
            this.identity = Preconditions.CheckNotNull(identity);
            this.router = Preconditions.CheckNotNull(router);
            this.dispatcher = Preconditions.CheckNotNull(dispatcher);
            this.connectionManager = Preconditions.CheckNotNull(connectionManager);
            this.cloudProxy = Preconditions.CheckNotNull(cloudProxy);            
        }

        public Task<object> CallMethod(string methodName, object parameters, string deviceId)
        {
            return this.dispatcher.CallMethod(methodName, parameters, deviceId);
        }

        public void BindDeviceProxy(IDeviceProxy deviceProxy)
        {
            ICloudListener cloudListener = new CloudListener(deviceProxy);
            this.cloudProxy.BindCloudListener(cloudListener);
            this.connectionManager.AddDeviceConnection(this.identity, deviceProxy);
        }

        public Task CloseAsync()
        {
            return this.connectionManager.CloseConnection(this.identity.Id);
        }

        public Task<Twin> GetTwin(string deviceId)
        {
            throw new NotImplementedException();
        }

        public Task ReceiveMessage(IMessage message)
        {
            var moduleIdentity = this.identity as IModuleIdentity;
            if (moduleIdentity != null)
            {
                message.Properties[ModuleIdPropertyName] = moduleIdentity.ModuleId;
            }
            return this.router.RouteMessage(message, this.identity.Id);
        }

        public Task ReceiveMessageBatch(IEnumerable<IMessage> messages) => this.router.RouteMessageBatch(messages, this.identity.Id);

        public Task UpdateReportedProperties(TwinCollection reportedProperties, string deviceId)
        {
            throw new NotImplementedException();
        }
    }
}
