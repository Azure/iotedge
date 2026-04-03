using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.ModuleClientWrapper
{
    public class BasicModuleClientWrapper : IAsyncDisposable, IModuleClientWrapper
    {
        IotHubModuleClient inner;
        SemaphoreSlim moduleClientLock;

        public BasicModuleClientWrapper(IotHubModuleClient moduleClient, SemaphoreSlim moduleClientLock)
        {
            this.inner = moduleClient;
            this.moduleClientLock = Preconditions.CheckNotNull(moduleClientLock);
        }

        public static async Task<BasicModuleClientWrapper> BuildModuleClientWrapperAsync()
        {
            SemaphoreSlim moduleClientLock = new SemaphoreSlim(1, 1);
            IotHubModuleClient moduleClient = await InitializeModuleClientAsync();
            return new BasicModuleClientWrapper(moduleClient, moduleClientLock);
        }

        public async Task RecreateClientAsync()
        {

            await this.moduleClientLock.WaitAsync();

            try
            {
                await this.inner.DisposeAsync();
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

        public async Task SendMessageAsync(string outputName, TelemetryMessage message)
        {
            await this.moduleClientLock.WaitAsync();

            try
            {
                await this.inner.SendMessageToRouteAsync(outputName, message);
                LoggerUtil.Writer.LogInformation("Successfully sent metrics via IoT message");
            }
            catch (Exception)
            {
                this.moduleClientLock.Release();
                throw;
            }

            this.moduleClientLock.Release();
        }

        public async ValueTask DisposeAsync()
        {
            await this.inner.DisposeAsync();
            this.moduleClientLock.Dispose();
        }

        static async Task<IotHubModuleClient> InitializeModuleClientAsync()
        {
            LoggerUtil.Writer.LogInformation("Trying to initialize module client using transport type [Amqp_Tcp_Only]");

            var options = new IotHubClientOptions(new IotHubClientAmqpSettings(IotHubClientTransportProtocol.Tcp))
            {
                AdditionalUserAgentInfo = Constants.ProductInfo
            };
            IotHubModuleClient moduleClient = await IotHubModuleClient.CreateFromEnvironmentAsync(options);

            await moduleClient.OpenAsync();
            LoggerUtil.Writer.LogInformation("Successfully initialized module client using transport type [Amqp_Tcp_Only]");
            return moduleClient;
        }
    }
}
