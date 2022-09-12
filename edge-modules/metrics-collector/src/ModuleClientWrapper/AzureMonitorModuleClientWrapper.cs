using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.ModuleClientWrapper
{
    public class AzureMonitorClientWrapper : IDisposable, IModuleClientWrapper
    {
        Option<BasicModuleClientWrapper> inner;

        public AzureMonitorClientWrapper(Option<BasicModuleClientWrapper> basicModuleClientWrapper)
        {
            this.inner = basicModuleClientWrapper;
        }

        public static Task<AzureMonitorClientWrapper> BuildModuleClientWrapperAsync()
        {
            // Don't try to initialize client so this call won't block.
            return Task.FromResult(new AzureMonitorClientWrapper(Option.None<BasicModuleClientWrapper>()));
        }

        public async Task RecreateClientAsync()
        {
            await this.inner.Match(async (BasicModuleClientWrapper) =>
            {
                try
                {
                    await BasicModuleClientWrapper.RecreateClientAsync();
                }
                catch (Exception)
                {
                    this.inner = Option.None<BasicModuleClientWrapper>();
                }
            }, async () =>
            {
                try
                {
                    this.inner = Option.Some(await BasicModuleClientWrapper.BuildModuleClientWrapperAsync());
                }
                catch (Exception)
                {
                    this.inner = Option.None<BasicModuleClientWrapper>();
                }
            });
        }

        public Task SendMessageAsync(string outputName, Message message)
        {
            throw new Exception("Not expected to send metrics to IoT Hub when upload target is AzureMonitor");
        }

        public void Dispose()
        {
            this.inner.ForEach((basicModuleClientWrapper) =>
            {
                basicModuleClientWrapper.Dispose();
            });
        }
    }
}



