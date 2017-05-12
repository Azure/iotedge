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
        readonly IRouter router;
        readonly IDispatcher dispatcher;
        readonly IConnectionManager connectionManager;
        readonly ICloudProxy cloudProxy;

        public DeviceListener(IIdentity identity, IRouter router, IDispatcher dispatcher, IConnectionManager connectionManager, ICloudProxy cloudProxy)
        {
            this.Identity = Preconditions.CheckNotNull(identity);
            this.router = Preconditions.CheckNotNull(router);
            this.dispatcher = Preconditions.CheckNotNull(dispatcher);
            this.connectionManager = Preconditions.CheckNotNull(connectionManager);
            this.cloudProxy = Preconditions.CheckNotNull(cloudProxy);            
        }

        public IIdentity Identity { get; }

        public Task<object> CallMethod(string methodName, object parameters, string deviceId)
        {
            return this.dispatcher.CallMethod(methodName, parameters, deviceId);
        }

        public void BindDeviceProxy(IDeviceProxy deviceProxy)
        {
            ICloudListener cloudListener = new CloudListener(deviceProxy);
            this.cloudProxy.BindCloudListener(cloudListener);
            this.connectionManager.AddDeviceConnection(this.Identity, deviceProxy);
        }

        public Task CloseAsync()
        {
            return this.connectionManager.CloseConnection(this.Identity.Id);
        }

        public Task ReceiveFeedbackMessage(IFeedbackMessage feedbackMessage)
        {
            return this.cloudProxy.SendFeedbackMessage(feedbackMessage);
        }

        public Task<Twin> GetTwin()
        {
            return this.cloudProxy.GetTwin();
        }

        public Task ReceiveMessage(IMessage message)
        {
            var moduleIdentity = this.Identity as IModuleIdentity;
            if (moduleIdentity != null)
            {
                message.Properties[ModuleIdPropertyName] = moduleIdentity.ModuleId;
            }
            return this.router.RouteMessage(message, this.Identity.Id);
        }

        public Task ReceiveMessageBatch(IEnumerable<IMessage> messages) => this.router.RouteMessageBatch(messages, this.Identity.Id);

        public Task UpdateReportedProperties(TwinCollection reportedProperties)
        {
            throw new NotImplementedException();
        }
    }
}
