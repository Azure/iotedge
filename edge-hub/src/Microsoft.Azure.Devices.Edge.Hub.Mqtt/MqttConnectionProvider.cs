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
        readonly IByteBufferConverter byteBufferConverter;

        public MqttConnectionProvider(IConnectionProvider connectionProvider, IMessageConverter<IProtocolGatewayMessage> pgMessageConverter, IByteBufferConverter byteBufferConverter)
        {
            this.connectionProvider = Preconditions.CheckNotNull(connectionProvider, nameof(connectionProvider));
            this.pgMessageConverter = Preconditions.CheckNotNull(pgMessageConverter, nameof(pgMessageConverter));
            this.byteBufferConverter = Preconditions.CheckNotNull(byteBufferConverter, nameof(byteBufferConverter));
        }

        public async Task<IMessagingBridge> Connect(IDeviceIdentity deviceidentity)
        {
            if (!(deviceidentity is ProtocolGatewayIdentity protocolGatewayIdentity))
            {
                throw new AuthenticationException("Invalid identity object received");
            }

            IDeviceListener deviceListener = await this.connectionProvider.GetDeviceListenerAsync(protocolGatewayIdentity.ClientCredentials.Identity);
            IMessagingServiceClient messagingServiceClient = new MessagingServiceClient(deviceListener, this.pgMessageConverter, this.byteBufferConverter);
            IMessagingBridge messagingBridge = new SingleClientMessagingBridge(deviceidentity, messagingServiceClient);

            return messagingBridge;
        }

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.connectionProvider?.Dispose();
            }
        }
    }
}
