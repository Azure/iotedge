// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Security.Authentication;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using IDeviceIdentity = Microsoft.Azure.Devices.ProtocolGateway.Identity.IDeviceIdentity;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    public class MqttConnectionProvider : IMqttConnectionProvider
    {
        readonly IConnectionProvider connectionProvider;
        readonly IMessageConverter<IProtocolGatewayMessage> pgMessageConverter;

        public MqttConnectionProvider(IConnectionProvider connectionProvider, IMessageConverter<IProtocolGatewayMessage> pgMessageConverter)
        {
            this.connectionProvider = Preconditions.CheckNotNull(connectionProvider, nameof(connectionProvider));
            this.pgMessageConverter = Preconditions.CheckNotNull(pgMessageConverter, nameof(pgMessageConverter));
        }

        public async Task<IMessagingBridge> Connect(IDeviceIdentity deviceidentity)
        {
            if (!(deviceidentity is ProtocolGatewayIdentity protocolGatewayIdentity))
            {
                throw new AuthenticationException("Invalid identity object received");
            }

            IDeviceListener deviceListener = await this.connectionProvider.GetDeviceListenerAsync(protocolGatewayIdentity.Identity);
            IMessagingServiceClient messagingServiceClient = new MessagingServiceClient(deviceListener, this.pgMessageConverter);
            IMessagingBridge messagingBridge = new SingleClientMessagingBridge(deviceidentity, messagingServiceClient);

            return messagingBridge;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.connectionProvider?.Dispose();
            }
        }

        public void Dispose() => this.Dispose(true);
    }
}
