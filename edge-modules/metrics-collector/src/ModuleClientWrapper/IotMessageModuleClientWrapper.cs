using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.ModuleClientWrapper
{
    public class IotMessageModuleClientWrapper : IDisposable, IModuleClientWrapper
    {
        BasicModuleClientWrapper inner;
        CancellationTokenSource cancellationTokenSource;

        public IotMessageModuleClientWrapper(BasicModuleClientWrapper basicModuleClientWrapper, CancellationTokenSource cts)
        {
            this.inner = basicModuleClientWrapper;
            this.cancellationTokenSource = cts;
        }

        public static async Task<IModuleClientWrapper> BuildModuleClientWrapperAsync(CancellationTokenSource cts)
        {
            BasicModuleClientWrapper basicModuleClientWrapper = await BasicModuleClientWrapper.BuildModuleClientWrapperAsync();
            return new IotMessageModuleClientWrapper(basicModuleClientWrapper, cts);
        }

        public async Task RecreateClientAsync()
        {
            try
            {
                await this.inner.RecreateClientAsync();
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogError($"Failed closing and re-establishing connection to IoT Hub: {e.ToString()}");
                this.cancellationTokenSource.Cancel();
            }
        }

        public async Task SendMessageAsync(string outputName, Message message)
        {
            try
            {
                await this.inner.SendMessageAsync(outputName, message);
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogError($"Failed sending metrics as IoT message: {e.ToString()}");
            }
        }

        public void Dispose()
        {
            this.inner.Dispose();
        }
    }
}



