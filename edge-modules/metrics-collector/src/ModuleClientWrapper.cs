using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{

    public class ModuleClientWrapper : IDisposable
    {
        ModuleClient inner;
        SemaphoreSlim moduleClientLock;
        TransportType transportType;

        public ModuleClientWrapper(ModuleClient moduleClient, TransportType transportType, SemaphoreSlim moduleClientLock)
        {
            this.inner = Preconditions.CheckNotNull(moduleClient);
            this.transportType = Preconditions.CheckNotNull(transportType);
            this.moduleClientLock = Preconditions.CheckNotNull(moduleClientLock);
        }

        public static async Task<ModuleClientWrapper> BuildModuleClientWrapperAsync(TransportType transportType)
        {
            SemaphoreSlim moduleClientLock = new SemaphoreSlim(1, 1);
            ModuleClient moduleClient = await InitializeModuleClientAsync(transportType);
            return new ModuleClientWrapper(moduleClient, transportType, moduleClientLock);
        }

        public async Task RecreateClientAsync()
        {
            await this.moduleClientLock.WaitAsync();

            try
            {
                this.inner.Dispose();
                this.inner = await InitializeModuleClientAsync(this.transportType);
                LoggerUtil.Writer.LogInformation("Closed and re-established connection to IoT Hub");
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogWarning($"Failed closing and re-establishing connection to IoT Hub: {e.ToString()}");
            }

            this.moduleClientLock.Release();
        }

        public async Task SendMessage(string outputName, Message message)
        {
            await this.moduleClientLock.WaitAsync();

            try
            {
                await this.inner.SendEventAsync(outputName, message);
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogError($"Failed sending metrics as IoT message: {e.ToString()}");
            }

            this.moduleClientLock.Release();
        }

        public void Dispose()
        {
            this.inner.Dispose();
            this.moduleClientLock.Dispose();
        }

        static async Task<ModuleClient> InitializeModuleClientAsync(TransportType transportType)
        {
            LoggerUtil.Writer.LogInformation($"Trying to initialize module client using transport type [{transportType}]");

            ITransportSettings[] GetTransportSettings()
            {
                switch (transportType)
                {
                    case TransportType.Mqtt:
                    case TransportType.Mqtt_Tcp_Only:
                        return new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) };
                    case TransportType.Mqtt_WebSocket_Only:
                        return new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_WebSocket_Only) };
                    case TransportType.Amqp_WebSocket_Only:
                        return new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only) };
                    default:
                        return new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
                }
            }

            ITransportSettings[] settings = GetTransportSettings();
            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            moduleClient.ProductInfo = Constants.ProductInfo;
            await moduleClient.OpenAsync();

            LoggerUtil.Writer.LogInformation($"Successfully initialized module client using transport type [{transportType}]");

            return moduleClient;
        }
    }
}
