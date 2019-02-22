// Copyright (c) Microsoft. All rights reserved.
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Test.TestServer.Controllers
{
    using System.IO;
    using System.Text;
    using Microsoft.Net.Http.Headers;

    public partial class Controller
    {
        const string TestLogs = @"[01/11/2019 07:45:35.466 AM] Edge Hub Main()
[01/11/2019 07:45:36.478 AM] Found intermediate certificates:
2019-01-10 23:45:38.945 -08:00 [INF] [Microsoft.Azure.Devices.Edge.Hub.Service.Modules.RoutingModule] - Using in-memory store
2019-01-10 23:45:39.026 -08:00 [DBG] [Microsoft.Azure.Devices.Edge.Hub.Core.IDeviceScopeIdentitiesCache] - Initializing device scope identities cache refresh task to run every 60 minutes.
2019-01-10 23:45:39.026 -08:00 [INF] [Microsoft.Azure.Devices.Edge.Hub.Core.IDeviceScopeIdentitiesCache] - Created device scope identities cache
2019-01-10 23:45:39.035 -08:00 [INF] [Microsoft.Azure.Devices.Edge.Hub.Core.IDeviceScopeIdentitiesCache] - Starting refresh of device scope identities cache
2019-01-10 23:45:39.037 -08:00 [DBG] [Microsoft.Azure.Devices.Edge.Hub.CloudProxy.ServiceProxy] - Created iterator to iterate all service identities in the scope of this IoT Edge device
2019-01-10 23:45:39.038 -08:00 [INF] [EdgeHub] - Starting Edge Hub
2019-01-10 23:45:39.039 -08:00 [INF] [EdgeHub] -
        █████╗ ███████╗██╗   ██╗██████╗ ███████╗
       ██╔══██╗╚══███╔╝██║   ██║██╔══██╗██╔════╝
       ███████║  ███╔╝ ██║   ██║██████╔╝█████╗
       ██╔══██║ ███╔╝  ██║   ██║██╔══██╗██╔══╝
       ██║  ██║███████╗╚██████╔╝██║  ██║███████╗
       ╚═╝  ╚═╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚══════╝

 ██╗ ██████╗ ████████╗    ███████╗██████╗  ██████╗ ███████╗
 ██║██╔═══██╗╚══██╔══╝    ██╔════╝██╔══██╗██╔════╝ ██╔════╝
 ██║██║   ██║   ██║       █████╗  ██║  ██║██║  ███╗█████╗
 ██║██║   ██║   ██║       ██╔══╝  ██║  ██║██║   ██║██╔══╝
 ██║╚██████╔╝   ██║       ███████╗██████╔╝╚██████╔╝███████╗
 ╚═╝ ╚═════╝    ╚═╝       ╚══════╝╚═════╝  ╚═════╝ ╚══════╝

2019-01-10 23:45:39.044 -08:00 [INF] [EdgeHub] - Version - 1.0.6-dev.BUILDNUMBER (COMMITID)
2019-01-10 23:45:39.065 -08:00 [INF] [EdgeHub] - Loaded server certificate with expiration date of ""2039-12-31T15:59:59.0000000-08:00""
2019-01-10 23:45:39.146 -08:00 [INF] [Microsoft.Azure.Devices.Edge.Hub.Core.Storage.MessageStore] - Created new message store
2019-01-10 23:45:39.147 -08:00 [INF] [Microsoft.Azure.Devices.Edge.Hub.Core.Storage.MessageStore] - Started task to cleanup processed and stale messages
2019-01-10 23:45:39.392 -08:00 [DBG] [Microsoft.Azure.Devices.Edge.Hub.CloudProxy.DeviceConnectivityManager] - Created DeviceConnectivityManager with connected check frequency 00:05:00 and disconnected check frequency 00:02:00
2019-01-10 23:45:39.431 -08:00 [DBG] [Microsoft.Azure.Devices.Edge.Hub.CloudProxy.DeviceConnectivityManager] - ConnectionManager provided
2019-01-10 23:45:39.435 -08:00 [INF] [EdgeHub] - Initializing configuration
";

        [HttpGet]
        [Route("modules/{name}/logs")]
        public FileStreamResult ModuleLogsAsync(string api_version, string name, bool follow, string tail)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(TestLogs);
            var ms = new MemoryStream(bytes);
            return new FileStreamResult(ms, new MediaTypeHeaderValue("text/plain"));            
        }
    }
}
