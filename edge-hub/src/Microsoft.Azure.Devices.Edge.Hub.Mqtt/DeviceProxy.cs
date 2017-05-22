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

        public Task CloseAsync(Exception ex)
        {
            if (this.isActive.GetAndSet(false))
            {
                this.channel.Close(ex ?? new EdgeHubConnectionException($"Connection closed for device {this.Identity.Id}."));
            }
            return TaskEx.Done;
        }

        public void SetInactive()
        {
            this.isActive.Set(false);
        }

        public Task<bool> SendMessageAsync(IMessage message)
        {
            message.SystemProperties[TemplateParameters.DeviceIdTemplateParam] = this.Identity.Id;
            IProtocolGatewayMessage pgMessage = this.messageConverter.FromMessage(message);
            
            this.channel.Handle(pgMessage);
            return Task.FromResult(true);
        }

        public Task<bool> SendMessage(IMessage message, string endpoint)
        {
            throw new NotImplementedException();
        }

        // TODO - Need to figure out how to do this. Don't see the API on the channel
        public Task<object> CallMethodAsync(string method, object parameters)
        {
            throw new System.NotImplementedException();
        }

        public bool IsActive => this.isActive.Get();

        public IIdentity Identity { get; }
    }
}