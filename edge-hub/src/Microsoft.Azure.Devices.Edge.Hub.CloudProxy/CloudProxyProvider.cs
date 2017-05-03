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
        readonly ILogger logger;
        readonly ITransportSettings[] transportSettings;
        readonly IMessageConverter<Message> messageConverter;

        public CloudProxyProvider(ILogger logger, IMessageConverter<Message> messageConverter, uint connectionPoolSize)
        {
            Preconditions.CheckRange((int)connectionPoolSize, 1, nameof(connectionPoolSize));
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.transportSettings = new ITransportSettings[] {
                new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                {
                    AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                    {
                        Pooling = connectionPoolSize > 1,
                        MaxPoolSize = connectionPoolSize
                    }
                }
            };
        }

        public async Task<Try<ICloudProxy>> Connect(string connectionString)
        {
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));

            Try<DeviceClient> tryDeviceClient = await this.ConnectToIoTHub(connectionString);
            if (!tryDeviceClient.Success)
            {
                return Try<ICloudProxy>.Failure(tryDeviceClient.Exception);
            }

            DeviceClient deviceClient = tryDeviceClient.Value;
            ICloudProxy cloudProxy = new CloudProxy(deviceClient, this.messageConverter, this.logger);
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
                this.logger.LogError(0, ex, $"Error connecting to IoTHub with connection string {connectionString}");
                return Try<DeviceClient>.Failure(ex);
            }
        }
    }
}
