// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
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

            Try<DeviceClient> tryDeviceClient = await this.ConnectToIoTHub(identity.Id, identity.ConnectionString);
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

        async Task<Try<DeviceClient>> ConnectToIoTHub(string id, string connectionString)
        {
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            try
            {
                DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, this.transportSettings);
                if (!useDefaultOperationTimeout)
                {
                    Events.SetDeviceClientTimeout(id, DefaultOperationTimeoutMilliseconds);
                    deviceClient.OperationTimeoutInMilliseconds = DefaultOperationTimeoutMilliseconds;
                }
                await deviceClient.OpenAsync();
                return deviceClient;
            }
            catch (Exception ex)
            {
                return Try<DeviceClient>.Failure(ex);
            }
        }        

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudProxyProvider>();
            const int IdStart = CloudProxyEventIds.CloudProxyProvider;

            enum EventIds
            {
                CloudConnectError = IdStart,
                CloudConnect,
                SetDeviceClientTimeout
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
