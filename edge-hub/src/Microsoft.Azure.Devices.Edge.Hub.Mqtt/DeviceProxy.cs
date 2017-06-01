// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IProtocolGatewayMessage = ProtocolGateway.Messaging.IMessage;

    public class DeviceProxy : IDeviceProxy
    {
        readonly IMessagingChannel<IProtocolGatewayMessage> channel;
        readonly IMessageConverter<IProtocolGatewayMessage> messageConverter;
        readonly AtomicBoolean isActive;

        public DeviceProxy(IMessagingChannel<IProtocolGatewayMessage> channel, IIdentity identity, IMessageConverter<IProtocolGatewayMessage> messageConverter)
        {
            this.isActive = new AtomicBoolean(true);
            this.channel = Preconditions.CheckNotNull(channel, nameof(channel));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.Identity = Preconditions.CheckNotNull(identity, nameof(this.Identity));
        }

        public IIdentity Identity { get; }

        public Task CloseAsync(Exception ex)
        {
            if (this.isActive.GetAndSet(false))
            {
                this.channel.Close(ex ?? new EdgeHubConnectionException($"Connection closed for device {this.Identity.Id}."));
                Events.Close(this.Identity);
            }
            return TaskEx.Done;
        }

        public void SetInactive()
        {
            this.isActive.Set(false);
            Events.SetInactive(this.Identity);
        }

        public Task<bool> SendMessageAsync(IMessage message)
        {
            message.SystemProperties[TemplateParameters.DeviceIdTemplateParam] = this.Identity.Id;
            message.SystemProperties[SystemProperties.OutboundUri] = Constants.OutboundUriC2D;
            IProtocolGatewayMessage pgMessage = this.messageConverter.FromMessage(message);

            this.channel.Handle(pgMessage);
            Events.SendMessage(this.Identity);
            return Task.FromResult(true);
        }

        public Task<bool> SendMessageAsync(IMessage message, string endpoint)
        {
            bool result = false;
            var moduleIdentity = this.Identity as IModuleIdentity;
            if (moduleIdentity != null)
            {
                message.SystemProperties[TemplateParameters.DeviceIdTemplateParam] = moduleIdentity.DeviceId;
                message.SystemProperties[SystemProperties.ModuleId] = moduleIdentity.ModuleId;
                message.SystemProperties[SystemProperties.EndpointId] = endpoint;
                message.SystemProperties[SystemProperties.OutboundUri] = Constants.OutboundUriModuleEndpoint;
                IProtocolGatewayMessage pgMessage = this.messageConverter.FromMessage(message);
                this.channel.Handle(pgMessage);
                result = true;
            }
            return Task.FromResult(result);
        }

        public Task CallMethodAsync(DirectMethodRequest request)
        {
            string address = TwinAddressHelper.FormatDeviceMethodRequestAddress(request.Id, request.Name);
            IProtocolGatewayMessage pgMessage = new ProtocolGatewayMessage.Builder(request.Data.ToByteBuffer(), address)
                .WithCreatedTimeUtc(DateTime.UtcNow)
                .Build();

            this.channel.Handle(pgMessage);
            return TaskEx.Done;
        }

        public Task OnDesiredPropertyUpdates(IMessage desiredProperties)
        {
            desiredProperties.SystemProperties[SystemProperties.OutboundUri] =
                Constants.OutboundUriTwinDesiredPropertyUpdate;

            this.channel.Handle(this.messageConverter.FromMessage(desiredProperties));

            return Task.FromResult(true);
        }

        public bool IsActive => this.isActive.Get();

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceProxy>();
            const int IdStart = MqttEventIds.DeviceProxy;

            enum EventIds
            {
                Close = IdStart,
                SetInactive,
                SendMessage
            }

            public static void Close(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.Close, Invariant($"Closing device proxy for device Id {identity.Id}"));
            }

            public static void SetInactive(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.SetInactive, Invariant($"Setting device proxy inactive for device Id {identity.Id}"));
            }

            public static void SendMessage(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.SendMessage, Invariant($"Sending message to device for device Id {identity.Id}"));
            }
        }
    }
}