using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{

    public class IothubConnectionManager
    {
        ModuleClientWrapper ModuleClientWrapper;
        SemaphoreSlim Semaphore;

        public IothubConnectionManager(ModuleClientWrapper moduleClient, SemaphoreSlim semaphore)
        {
            this.ModuleClientWrapper = moduleClient;
            this.Semaphore = semaphore;
        }

        public async Task ConnectToIothub()
        {
            await this.Semaphore.WaitAsync();
            await this.ModuleClientWrapper.RecreateClient();
            this.Semaphore.Release();
        }
    }
}