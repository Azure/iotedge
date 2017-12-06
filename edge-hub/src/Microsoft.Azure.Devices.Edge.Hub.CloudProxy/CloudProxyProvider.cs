// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class CloudProxyProvider : ICloudProxyProvider
    {
        const uint DefaultOperationTimeoutMilliseconds = 1 * 60 * 1000; // 1 min
        readonly ITransportSettings[] transportSettings;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly bool useDefaultOperationTimeout;

        public CloudProxyProvider(IMessageConverterProvider messageConverterProvider, int connectionPoolSize, bool useDefaultOperationTimeout)
        {
            Preconditions.CheckRange(connectionPoolSize, 1, nameof(connectionPoolSize));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.useDefaultOperationTimeout = useDefaultOperationTimeout;
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

        public async Task<Try<ICloudProxy>> Connect(IIdentity identity, Action<ConnectionStatus, ConnectionStatusChangeReason> connectionStatusChangedHandler)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));

            Try<DeviceClient> tryDeviceClient = await this.ConnectToIoTHub(identity.Id, identity.ConnectionString, identity.ProductInfo, connectionStatusChangedHandler);
            if (!tryDeviceClient.Success)
            {
                Events.ConnectError(identity.Id, tryDeviceClient.Exception);
                return Try<ICloudProxy>.Failure(tryDeviceClient.Exception);
            }

            Events.ConnectSuccess(identity.Id);
            DeviceClient deviceClient = tryDeviceClient.Value;
            ICloudProxy cloudProxy = new CloudProxy(deviceClient, this.messageConverterProvider, identity, connectionStatusChangedHandler);
            return Try.Success(cloudProxy);
        }

        async Task<Try<DeviceClient>> ConnectToIoTHub(string id, string connectionString, string productInfo, Action<ConnectionStatus, ConnectionStatusChangeReason> connectionStatusChangedHandler)
        {
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            try
            {
                // The device SDK doesn't appear to be falling back to WebSocket from TCP,
                // so we'll do it explicitly until we can get the SDK sorted out.
                return await Fallback.ExecuteAsync(
                    () => this.CreateAndOpenDeviceClient(id, connectionString, productInfo, this.transportSettings[0], connectionStatusChangedHandler),
                    () => this.CreateAndOpenDeviceClient(id, connectionString, productInfo, this.transportSettings[1], connectionStatusChangedHandler));

                // TODO: subsequent links will still try AMQP first, then fall back to AMQP over WebSocket. In the worst
                // case, an edge device might end up with one connection pool for AMQP and one for AMQP over WebSocket. Once
                // a first connection is made, should subsequent connections only try one protocol (the protocol that the 1st
                // connection used)?
            }
            catch (Exception ex)
            {
                return Try<DeviceClient>.Failure(ex);
            }
        }

        async Task<DeviceClient> CreateAndOpenDeviceClient(string id, string connectionString, string productInfo, ITransportSettings transportSettings, Action<ConnectionStatus, ConnectionStatusChangeReason> connectionStatusChangedHandler)
        {
            Events.AttemptingConnectionWithTransport(transportSettings.GetTransportType());
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, new ITransportSettings[] { transportSettings });
            if (!this.useDefaultOperationTimeout)
            {
                Events.SetDeviceClientTimeout(id, DefaultOperationTimeoutMilliseconds);
                deviceClient.OperationTimeoutInMilliseconds = DefaultOperationTimeoutMilliseconds;
            }
            deviceClient.ProductInfo = productInfo;
            if (connectionStatusChangedHandler != null)
            {
                deviceClient.SetConnectionStatusChangesHandler(new ConnectionStatusChangesHandler(connectionStatusChangedHandler));
            }
            await deviceClient.OpenAsync();
            Events.ConnectedWithTransport(transportSettings.GetTransportType());
            return deviceClient;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudProxyProvider>();
            const int IdStart = CloudProxyEventIds.CloudProxyProvider;

            enum EventIds
            {
                CloudConnectError = IdStart,
                CloudConnect,
                SetDeviceClientTimeout,
                AttemptingTransport,
                TransportConnected
            }

            static string TransportName(TransportType type)
            {
                Preconditions.CheckArgument(type == TransportType.Amqp_Tcp_Only || type == TransportType.Amqp_WebSocket_Only);
                return type == TransportType.Amqp_Tcp_Only ? "AMQP" : "AMQP over WebSocket";
            }

            public static void AttemptingConnectionWithTransport(TransportType transport)
            {
                Log.LogInformation((int)EventIds.AttemptingTransport, $"Attempting to connect to IoT Hub via {TransportName(transport)}...");
            }

            public static void ConnectedWithTransport(TransportType transport)
            {
                Log.LogInformation((int)EventIds.TransportConnected, $"Connected to IoT Hub via {TransportName(transport)}.");
            }

            public static void ConnectError(string id, Exception ex)
            {
                Log.LogError((int)EventIds.CloudConnectError, ex, Invariant($"Error opening cloud connection for device {id}"));
            }

            public static void ConnectSuccess(string id)
            {
                Log.LogInformation((int)EventIds.CloudConnect, Invariant($"Opened new cloud connection for device {id}"));
            }

            public static void SetDeviceClientTimeout(string id, uint timeout)
            {
                Log.LogDebug((int)EventIds.SetDeviceClientTimeout, Invariant($"Setting device client timeout for {id} to {timeout}"));
            }
        }
    }
}
