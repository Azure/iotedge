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
            var iotHubDeviceIdentity = deviceidentity as Identity;
            if (iotHubDeviceIdentity == null)
            {
                throw new AuthenticationException("Invalid identity object received");
            }

            IDeviceListener deviceListener = await this.connectionProvider.GetDeviceListenerAsync(iotHubDeviceIdentity);
            IMessagingServiceClient messagingServiceClient = new MessagingServiceClient(deviceListener, this.pgMessageConverter);
            var messagingBridge = new SingleClientMessagingBridge(deviceidentity, messagingServiceClient);

            return messagingBridge;
        }
    }
}
