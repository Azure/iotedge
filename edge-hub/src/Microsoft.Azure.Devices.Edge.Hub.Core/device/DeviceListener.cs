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

        public Task<object> CallMethodAsync(string methodName, object parameters, string deviceId)
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
            return this.connectionManager.CloseConnectionAsync(this.Identity.Id);
        }

        public Task ProcessFeedbackMessageAsync(IFeedbackMessage feedbackMessage)
        {
            return this.cloudProxy.SendFeedbackMessageAsync(feedbackMessage);
        }

        public Task<Twin> GetTwinAsync()
        {
            return this.cloudProxy.GetTwinAsync();
        }

        public Task ProcessMessageAsync(IMessage message)
        {
            var moduleIdentity = this.Identity as IModuleIdentity;
            if (moduleIdentity != null)
            {
                message.Properties[ModuleIdPropertyName] = moduleIdentity.ModuleId;
            }
            return this.router.RouteMessage(message, this.Identity.Id);
        }

        public Task ProcessMessageBatchAsync(IEnumerable<IMessage> messages) => this.router.RouteMessageBatch(messages, this.Identity.Id);

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties)
        {
            throw new NotImplementedException();
        }
    }
}
