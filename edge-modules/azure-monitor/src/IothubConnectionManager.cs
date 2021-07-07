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

        public async Task ConnectToIothub(CancellationToken ct)
        {
            await this.Semaphore.WaitAsync();
            Console.WriteLine($"connection manager before recreate {this.ModuleClientWrapper.Inner}");
            await this.ModuleClientWrapper.RecreateClient(ct);
            Console.WriteLine($"connection manager after recreate {this.ModuleClientWrapper.Inner}");
            this.Semaphore.Release();
        }
    }
}