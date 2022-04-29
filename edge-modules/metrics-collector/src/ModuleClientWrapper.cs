using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{

    public class ModuleClientWrapper : IDisposable
    {
        ModuleClient inner;
        ITransportSettings[] transportSettings;
        SemaphoreSlim moduleClientLock;

        public ModuleClientWrapper(ModuleClient moduleClient, ITransportSettings[] transportSettings, SemaphoreSlim moduleClientLock)
        {
            this.inner = Preconditions.CheckNotNull(moduleClient);
            this.transportSettings = Preconditions.CheckNotNull(transportSettings);
            this.moduleClientLock = Preconditions.CheckNotNull(moduleClientLock);
        }

        public static async Task<ModuleClientWrapper> BuildModuleClientWrapperAsync(ITransportSettings[] transportSettings)
        {
            SemaphoreSlim moduleClientLock = new SemaphoreSlim(1, 1);

            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(transportSettings);
            moduleClient.ProductInfo = Constants.ProductInfo;
            await moduleClient.OpenAsync();

            return new ModuleClientWrapper(moduleClient, transportSettings, moduleClientLock);
        }

        public async Task RecreateClientAsync()
        {
            await this.moduleClientLock.WaitAsync();

            try
            {
                this.inner.Dispose();
                this.inner = await ModuleClient.CreateFromEnvironmentAsync(this.transportSettings);
                this.inner.ProductInfo = Constants.ProductInfo;
                await this.inner.OpenAsync();

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
    }
}
