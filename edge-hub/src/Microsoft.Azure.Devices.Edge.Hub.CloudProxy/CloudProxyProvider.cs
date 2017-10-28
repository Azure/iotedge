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
        const uint DefaultOperationTimeoutMilliseconds = 4 * 60 * 1000; // 4 mins
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
                }
            };
        }

        public async Task<Try<ICloudProxy>> Connect(IIdentity identity, Action<ConnectionStatus, ConnectionStatusChangeReason> connectionStatusChangedHandler)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));

            Try<DeviceClient> tryDeviceClient = await this.ConnectToIoTHub(identity.ConnectionString);
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

        async Task<Try<DeviceClient>> ConnectToIoTHub(string connectionString)
        {
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            try
            {
                DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, this.transportSettings);
                if (!useDefaultOperationTimeout)
                {
                    deviceClient.OperationTimeoutInMilliseconds = GetOperationTimeoutMilliseconds(connectionString);
                }
                await deviceClient.OpenAsync();
                return deviceClient;
            }
            catch (Exception ex)
            {
                return Try<DeviceClient>.Failure(ex);
            }
        }

        internal static uint GetOperationTimeoutMilliseconds(string connectionString)
        {
            uint operationTimeoutInMilliseconds = 0;
            try
            {
                // Set the Operation timeout to the duration of the sas token, if any. 
                // That way if the network connection is lost for a bit, the SDK will retry 
                // till the connection comes back or the token expires. 
                var iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(connectionString);
                string hostName = iotHubConnectionStringBuilder.HostName;
                if (iotHubConnectionStringBuilder.AuthenticationMethod is DeviceAuthenticationWithToken deviceAuthenticationWithToken)
                {
                    SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(hostName, deviceAuthenticationWithToken.Token);
                    DateTime expiryTime = sharedAccessSignature.ExpiresOn.ToUniversalTime();
                    if (expiryTime > DateTime.UtcNow)
                    {
                        TimeSpan timeSpan = expiryTime - DateTime.UtcNow;
                        operationTimeoutInMilliseconds = (uint)timeSpan.TotalMilliseconds;
                    }
                }
            }
            catch // If anything goes wrong, ignore exception and return the default.
            { }
            return operationTimeoutInMilliseconds > DefaultOperationTimeoutMilliseconds ? operationTimeoutInMilliseconds : DefaultOperationTimeoutMilliseconds;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudProxyProvider>();
            const int IdStart = CloudProxyEventIds.CloudProxyProvider;

            enum EventIds
            {
                CloudConnectError = IdStart,
                CloudConnect
            }

            public static void ConnectError(string id, Exception ex)
            {
                Log.LogError((int)EventIds.CloudConnectError, ex, Invariant($"Error opening cloud connection for device {id}"));
            }

            public static void ConnectSuccess(string id)
            {
                Log.LogInformation((int)EventIds.CloudConnect, Invariant($"Opened new cloud connection for device {id}"));
            }
        }
    }
}
