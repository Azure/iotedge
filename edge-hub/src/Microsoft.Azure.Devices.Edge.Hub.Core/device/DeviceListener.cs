// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    class DeviceListener : IDeviceListener
    {
        readonly IEdgeHub edgeHub;
        readonly IConnectionManager connectionManager;
        readonly ICloudProxy cloudProxy;

        public DeviceListener(IIdentity identity, IEdgeHub edgeHub, IConnectionManager connectionManager, ICloudProxy cloudProxy)
        {
            this.Identity = Preconditions.CheckNotNull(identity);
            this.edgeHub = Preconditions.CheckNotNull(edgeHub);
            this.connectionManager = Preconditions.CheckNotNull(connectionManager);
            this.cloudProxy = Preconditions.CheckNotNull(cloudProxy);
        }

        public IIdentity Identity { get; }

        public Task ProcessMethodResponseAsync(DirectMethodResponse response) => this.edgeHub.SendMethodResponseAsync(response);

        public void BindDeviceProxy(IDeviceProxy deviceProxy)
        {
            ICloudListener cloudListener = new CloudListener(deviceProxy, this.edgeHub, this.Identity);
            this.cloudProxy.BindCloudListener(cloudListener);
            this.connectionManager.AddDeviceConnection(this.Identity, deviceProxy);
            Events.BindDeviceProxy(this.Identity);
        }

        public Task CloseAsync()
        {
            this.connectionManager.RemoveDeviceConnection(this.Identity.Id);
            Events.Close(this.Identity);
            return TaskEx.Done;
        }

        public Task ProcessFeedbackMessageAsync(IFeedbackMessage feedbackMessage)
        {
            Preconditions.CheckNotNull(feedbackMessage, nameof(feedbackMessage));
            return this.cloudProxy.SendFeedbackMessageAsync(feedbackMessage);
        }

        public Task<IMessage> GetTwinAsync() => this.cloudProxy.GetTwinAsync();

        public Task ProcessMessageAsync(IMessage message)
        {
            Preconditions.CheckNotNull(message, nameof(message));
            return this.edgeHub.ProcessDeviceMessage(this.Identity, message);
        }

        public Task ProcessMessageBatchAsync(IEnumerable<IMessage> messages)
        {
            List<IMessage> messagesList = Preconditions.CheckNotNull(messages, nameof(messages)).ToList();
            return this.edgeHub.ProcessDeviceMessageBatch(this.Identity, messagesList);
        }

        public Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage)
        {
            reportedPropertiesMessage.SystemProperties[SystemProperties.EnqueuedTime] = DateTime.UtcNow.ToString("o");
            reportedPropertiesMessage.SystemProperties[SystemProperties.MessageSchema] = Constants.TwinChangeNotificationMessageSchema;
            reportedPropertiesMessage.SystemProperties[SystemProperties.MessageType] = Constants.TwinChangeNotificationMessageType;

            switch (this.Identity)
            {
                case IModuleIdentity moduleIdentity:
                    reportedPropertiesMessage.SystemProperties[SystemProperties.ConnectionDeviceId] = moduleIdentity.DeviceId;
                    reportedPropertiesMessage.SystemProperties[SystemProperties.ConnectionModuleId] = moduleIdentity.ModuleId;
                    break;
                case IDeviceIdentity deviceIdentity:
                    reportedPropertiesMessage.SystemProperties[SystemProperties.ConnectionDeviceId] = deviceIdentity.DeviceId;
                    break;
            }
            return this.edgeHub.UpdateReportedPropertiesAsync(this.Identity, reportedPropertiesMessage);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceListener>();
            const int IdStart = HubCoreEventIds.DeviceListener;

            enum EventIds
            {
                BindDeviceProxy = IdStart,
                RemoveDeviceConnection
            }

            public static void BindDeviceProxy(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.BindDeviceProxy, Invariant($"Bind device proxy for device {identity.Id}"));
            }

            public static void Close(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.RemoveDeviceConnection, Invariant($"Remove device connection for device {identity.Id}"));
            }
        }
    }
}
