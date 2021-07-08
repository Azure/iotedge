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
        SemaphoreSlim ModuleClientLock;

        public IothubConnectionManager(ModuleClientWrapper moduleClient, SemaphoreSlim moduleClientLock)
        {
            this.ModuleClientWrapper = moduleClient;
            this.ModuleClientLock = moduleClientLock;
        }

        public async Task ConnectToIothub()
        {
            await this.ModuleClientLock.WaitAsync();
            await this.ModuleClientWrapper.RecreateClient();
            this.ModuleClientLock.Release();
        }
    }
}