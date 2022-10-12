using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.ModuleClientWrapper
{
    public interface IModuleClientWrapper
    {
        Task RecreateClientAsync();
        Task SendMessageAsync(string outputName, Message message);

    }
}
