// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class DeviceProxy : IDeviceProxy
    {
        readonly AtomicBoolean isActive;
        readonly IConnectionRegistry connectionRegistry;
        readonly ITwinHandler twinHandler;
        readonly IModuleToModuleMessageHandler moduleToModuleMessageHandler;
        readonly ICloud2DeviceMessageHandler cloud2DeviceMessageHandler;
        readonly IDirectMethodHandler directMethodHandler;

        public delegate DeviceProxy Factory(IIdentity identity, bool isDirectClient);

        public bool IsDirectClient { get; }

        public DeviceProxy(
                    IIdentity identity,
                    bool isDirectClient,
                    IConnectionRegistry connectionRegistry,
                    ITwinHandler twinHandler,
                    IModuleToModuleMessageHandler moduleToModuleMessageHandler,
                    ICloud2DeviceMessageHandler cloud2DeviceMessageHandler,
                    IDirectMethodHandler directMethodHandler)
        {
            this.Identity = Preconditions.CheckNotNull(identity);
            this.connectionRegistry = Preconditions.CheckNotNull(connectionRegistry);
            this.twinHandler = Preconditions.CheckNotNull(twinHandler);
            this.moduleToModuleMessageHandler = Preconditions.CheckNotNull(moduleToModuleMessageHandler);
            this.cloud2DeviceMessageHandler = Preconditions.CheckNotNull(cloud2DeviceMessageHandler);
            this.directMethodHandler = Preconditions.CheckNotNull(directMethodHandler);
            this.isActive = new AtomicBoolean(true);
            this.IsDirectClient = isDirectClient;

            // when a child edge device connects, it uses $edgeHub identity.
            // Although it is a direct client, it uses the indirect topics
            if (identity is ModuleIdentity moduleIdentity)
            {
                if (string.Equals(moduleIdentity.ModuleId, Constants.EdgeHubModuleId))
                {
                    this.IsDirectClient = false;
                }
            }

            Events.Created(this.Identity);
        }

        public bool IsActive => this.isActive.Get();

        public IIdentity Identity { get; }

        public async Task CloseAsync(Exception ex)
        {
            if (this.isActive.GetAndSet(false))
            {
                await this.connectionRegistry.CloseConnectionAsync(this.Identity);
                Events.Close(this.Identity, ex);
            }

            return;
        }

        public Task<Option<IClientCredentials>> GetUpdatedIdentity()
        {
            throw new NotImplementedException();
        }

        public Task<DirectMethodResponse> InvokeMethodAsync(DirectMethodRequest request)
        {
            Events.SendingDirectMethod(this.Identity);
            return this.directMethodHandler.CallDirectMethodAsync(request, this.Identity, this.IsDirectClient);
        }

        public Task OnDesiredPropertyUpdates(IMessage desiredProperties)
        {
            Events.SendingDesiredPropertyUpdate(this.Identity);
            return this.twinHandler.SendDesiredPropertiesUpdate(desiredProperties, this.Identity, this.IsDirectClient);
        }

        public Task SendC2DMessageAsync(IMessage message)
        {
            Events.SendingC2DMessage(this.Identity);
            return this.cloud2DeviceMessageHandler.SendC2DMessageAsync(message, this.Identity, this.IsDirectClient);
        }

        public Task SendMessageAsync(IMessage message, string input)
        {
            Events.SendingModuleToModuleMessage(this.Identity);
            return this.moduleToModuleMessageHandler.SendModuleToModuleMessageAsync(message, input, this.Identity, this.IsDirectClient);
        }

        public Task SendTwinUpdate(IMessage twin)
        {
            Events.SendingTwinUpdate(this.Identity);
            return this.twinHandler.SendTwinUpdate(twin, this.Identity, this.IsDirectClient);
        }

        public void SetInactive()
        {
            this.isActive.Set(false);
            Events.SetInactive(this.Identity);
        }

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.DeviceProxy;
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceProxy>();

            enum EventIds
            {
                Created = IdStart,
                Close,
                SetInactive,
                SendingTwinUpdate,
                SendingDesiredPropertyUpdate,
                SendingC2DMessage,
                SendingDirectMethod,
                SendingModuleToModuleMessage
            }

            public static void Created(IIdentity identity) => Log.LogInformation((int)EventIds.Created, $"Created device proxy for {identity.IotHubHostname}/{identity.Id}");
            public static void Close(IIdentity identity, Exception ex) => Log.LogInformation((int)EventIds.Close, ex, $"Closed device proxy for {identity.IotHubHostname}/{identity.Id}");
            public static void SetInactive(IIdentity identity) => Log.LogInformation((int)EventIds.Close, $"Inactivated device proxy for {identity.IotHubHostname}/{identity.Id}");
            public static void SendingTwinUpdate(IIdentity identity) => Log.LogDebug((int)EventIds.SendingTwinUpdate, $"Sending twin update to {identity.Id}");
            public static void SendingDesiredPropertyUpdate(IIdentity identity) => Log.LogDebug((int)EventIds.SendingDesiredPropertyUpdate, $"Sending desired property update to {identity.Id}");
            public static void SendingC2DMessage(IIdentity identity) => Log.LogDebug((int)EventIds.SendingC2DMessage, $"Sending C2D message to {identity.Id}");
            public static void SendingDirectMethod(IIdentity identity) => Log.LogDebug((int)EventIds.SendingDirectMethod, $"Sending direct method message to {identity.Id}");
            public static void SendingModuleToModuleMessage(IIdentity identity) => Log.LogDebug((int)EventIds.SendingModuleToModuleMessage, $"Sending module to module message to {identity.Id}");
        }
    }
}
