// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class CloudProxyProvider : ICloudProxyProvider
    {
        static readonly ITransportSettings[] AmqpTcpTransportSettings = 
            {
                new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                {
                    AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                    {
                        Pooling = true,
                        MaxPoolSize = 1
                    }
                }
            };

        readonly ILogger logger;
        readonly ITransportSettings[] transportSettings;
        readonly IMessageConverter<Message> messageConverter;

        public CloudProxyProvider(ILogger logger, ITransportSettings[] transportSettings, IMessageConverter<Message> messageConverter)
        {
            this.logger = logger;
            this.transportSettings = transportSettings;
            this.messageConverter = messageConverter;
        }

        public async Task<Try<ICloudProxy>> Connect(string connectionString, ICloudListener cloudListener)
        {
            Preconditions.CheckNotNull(cloudListener, nameof(cloudListener));
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));

            Try<DeviceClient> tryDeviceClient = await this.ConnectToIoTHub(connectionString);
            if (!tryDeviceClient.Success)
            {
                return Try<ICloudProxy>.Failure(tryDeviceClient.Exception);
            }

            DeviceClient deviceClient = tryDeviceClient.Value;        
            ICloudProxy cloudProxy = new CloudProxy(deviceClient, this.messageConverter, this.logger);
            ICloudReceiver cloudReceiver = new CloudReceiver(deviceClient);
            cloudReceiver.Init(cloudListener);
            return Try.Success(cloudProxy);
        }        

        async Task<Try<DeviceClient>> ConnectToIoTHub(string connectionString)
        {
            try
            {
                DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, this.transportSettings);
                deviceClient.RetryPolicy = RetryPolicyType.Exponential_Backoff_With_Jitter;
                await deviceClient.OpenAsync();
                return deviceClient;
            }
            catch (Exception ex)
            {
                // TODO - Check if it is okay to emit connection string in logs
                this.logger.LogError($"Error connecting to IoTHub with connection string {connectionString} - {ex.ToString()}");
                return Try<DeviceClient>.Failure(ex);
            }
        }
    }
}
