// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class CloudConnectionProvider : ICloudConnectionProvider
    {
        readonly ITransportSettings[] transportSettings;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly IDeviceClientProvider deviceClientProvider;

        public CloudConnectionProvider(IMessageConverterProvider messageConverterProvider, int connectionPoolSize, IDeviceClientProvider deviceClientProvider)
        {
            Preconditions.CheckRange(connectionPoolSize, 1, nameof(connectionPoolSize));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.deviceClientProvider = Preconditions.CheckNotNull(deviceClientProvider, nameof(deviceClientProvider));
            this.transportSettings = new ITransportSettings[] {
                new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                {
                    AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                    {
                        Pooling = true,
                        MaxPoolSize = (uint)connectionPoolSize
                    }
                },
                new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only)
                {
                    AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                    {
                        Pooling = true,
                        MaxPoolSize = (uint)connectionPoolSize
                    }
                }
            };
        }

        public async Task<Try<ICloudConnection>> Connect(IIdentity identity, Action<CloudConnectionStatus> connectionStatusChangedHandler)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));

            try
            {
                var cloudConnection = new CloudConnection(connectionStatusChangedHandler, this.transportSettings, this.messageConverterProvider, this.deviceClientProvider);
                await cloudConnection.CreateOrUpdateAsync(identity);
                Events.SuccessCreatingCloudConnection(identity);
                return Try.Success<ICloudConnection>(cloudConnection);
            }
            catch (Exception ex)
            {
                Events.ErrorCreatingCloudConnection(identity, ex);
                return Try<ICloudConnection>.Failure(ex);
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudConnection>();
            const int IdStart = CloudProxyEventIds.CloudConnectionProvider;

            enum EventIds
            {
                CloudConnectError = IdStart,
                CloudConnectSuccess
            }

            public static void SuccessCreatingCloudConnection(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.CloudConnectSuccess, $"Created cloud connection for client {identity.Id}");
            }

            public static void ErrorCreatingCloudConnection(IIdentity identity, Exception exception)
            {
                Log.LogWarning((int)EventIds.CloudConnectError, exception, $"Error creating cloud connection for client {identity.Id}");
            }
        }
    }
}
