using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{

    public class IothubConnectionManager
    {
        ModuleClient ModuleClient;
        public IothubConnectionManager(ModuleClient moduleClient)
        {
            this.ModuleClient = moduleClient;
        }

        public async Task ConnectToIothub()
        {
            try
            {
                await this.ModuleClient.OpenAsync();

                LoggerUtil.Writer.LogInformation("Successfully re-established connection to IoT Hub");
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogWarning("Failed re-establishing connection to IoT Hub", e);
            }
        }
    }
}