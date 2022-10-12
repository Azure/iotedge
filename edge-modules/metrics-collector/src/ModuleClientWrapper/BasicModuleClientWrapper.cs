using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.ModuleClientWrapper
{
    public class BasicModuleClientWrapper : IDisposable, IModuleClientWrapper
    {
        ModuleClient inner;
        SemaphoreSlim moduleClientLock;

        public BasicModuleClientWrapper(ModuleClient moduleClient, SemaphoreSlim moduleClientLock)
        {
            this.inner = moduleClient;
            this.moduleClientLock = Preconditions.CheckNotNull(moduleClientLock);
        }

        public static async Task<BasicModuleClientWrapper> BuildModuleClientWrapperAsync()
        {
            SemaphoreSlim moduleClientLock = new SemaphoreSlim(1, 1);
            ModuleClient moduleClient = await InitializeModuleClientAsync();
            return new BasicModuleClientWrapper(moduleClient, moduleClientLock);
        }

        public async Task RecreateClientAsync()
        {

            await this.moduleClientLock.WaitAsync();

            try
            {
                this.inner.Dispose();
                this.inner = await InitializeModuleClientAsync();
                LoggerUtil.Writer.LogInformation("Closed and re-established connection to IoT Hub");
            }
            catch (Exception)
            {
                this.moduleClientLock.Release();
                throw;
            }

            this.moduleClientLock.Release();
        }

        public async Task SendMessageAsync(string outputName, Message message)
        {
            await this.moduleClientLock.WaitAsync();

            try
            {
                await this.inner.SendEventAsync(outputName, message);
                LoggerUtil.Writer.LogInformation("Successfully sent metrics via IoT message");
            }
            catch (Exception)
            {
                this.moduleClientLock.Release();
                throw;
            }

            this.moduleClientLock.Release();
        }

        public void Dispose()
        {
            this.inner.Dispose();
            this.moduleClientLock.Dispose();
        }

        static async Task<ModuleClient> InitializeModuleClientAsync()
        {
            TransportType transportType = TransportType.Amqp_Tcp_Only;
            LoggerUtil.Writer.LogInformation($"Trying to initialize module client using transport type [{transportType}]");

            ITransportSettings[] settings = new ITransportSettings[] { new AmqpTransportSettings(transportType) };
            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            moduleClient.ProductInfo = Constants.ProductInfo;

            await moduleClient.OpenAsync();
            LoggerUtil.Writer.LogInformation($"Successfully initialized module client using transport type [{transportType}]");
            return moduleClient;
        }
    }
}

