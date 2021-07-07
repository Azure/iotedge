using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{

    public class ModuleClientWrapper
    {
        public ModuleClient Inner;
        ITransportSettings[] transportSettings;

        public ModuleClientWrapper(ModuleClient moduleClient, ITransportSettings[] transportSettings)
        {
            this.Inner = moduleClient;
            this.transportSettings = transportSettings;

            moduleClient.ProductInfo = Constants.ProductInfo;
        }

        public static async Task<ModuleClientWrapper> BuildModuleClientWrapperAsync(ITransportSettings[] transportSettings)
        {
            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(transportSettings);
            moduleClient.ProductInfo = Constants.ProductInfo;
            await moduleClient.OpenAsync();

            return new ModuleClientWrapper(moduleClient, transportSettings);
        }

        public async Task RecreateClient(CancellationToken ct)
        {
            try
            {
                await this.Inner.CloseAsync();
                this.Inner.Dispose();

                this.Inner = await ModuleClient.CreateFromEnvironmentAsync(this.transportSettings);
                this.Inner.ProductInfo = Constants.ProductInfo;
                await this.Inner.OpenAsync();

                LoggerUtil.Writer.LogInformation("Closed and re-established connection to IoT Hub");
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogWarning($"Failed closing and re-establishing connection to IoT Hub: {e.ToString()}");
            }
        }
    }
}
