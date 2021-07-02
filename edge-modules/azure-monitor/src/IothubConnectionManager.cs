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
                // Close the connection, wait 5 seconds, and reopen it.
                // Delay needed to assure SDK will reconnect.
                await this.ModuleClient.CloseAsync();
                await Task.Delay(TimeSpan.FromSeconds(5));
                await this.ModuleClient.OpenAsync();

                LoggerUtil.Writer.LogInformation("Closed and re-established connection to IoT Hub");
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogWarning("Failed closing and re-establishing connection to IoT Hub", e);
            }
        }
    }
}