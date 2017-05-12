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
    using PGMessage = ProtocolGateway.Messaging.IMessage;

    public class DeviceProxy : IDeviceProxy
    {
        readonly IMessagingChannel<PGMessage> channel;
        readonly IMessageConverter<PGMessage> messageConverter;
        readonly AtomicBoolean isActive;

        public DeviceProxy(IMessagingChannel<PGMessage> channel, IIdentity identity, IMessageConverter<PGMessage> messageConverter)
        {
            this.isActive = new AtomicBoolean(true);
            this.channel = Preconditions.CheckNotNull(channel, nameof(channel));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.Identity = Preconditions.CheckNotNull(identity, nameof(this.Identity));
        }

        public Task Close(Exception ex)
        {
            if (this.isActive.GetAndSet(false))
            {
                this.channel.Close(ex ?? new InvalidOperationException("Not able to start DeviceListener properly."));
            }
            return TaskEx.Done;
        }

        public Task SendMessage(IMessage message)
        {
            message.SystemProperties[TemplateParameters.DeviceIdTemplateParam] = this.Identity.Id;
            PGMessage pgMessage = this.messageConverter.FromMessage(message);
            
            this.channel.Handle(pgMessage);
            return TaskEx.Done;
        }

        // TODO - Need to figure out how to do this. Don't see the API on the channel
        public Task<object> CallMethod(string method, object parameters)
        {
            throw new System.NotImplementedException();
        }

        public bool IsActive => this.isActive.Get();

        public IIdentity Identity { get; }
    }
}