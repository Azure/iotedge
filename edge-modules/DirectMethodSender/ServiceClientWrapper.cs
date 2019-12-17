// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Logging;
    using TransportType = Microsoft.Azure.Devices.TransportType;

    public class ServiceClientWrapper : IDirectMethodClient
    {

        public readonly string ConnectionString;
        public readonly TransportType TransportType;
        public readonly ILogger Logger;
        ServiceClient serviceClient = null;
        int directMethodCount = 1;

        private ServiceClientWrapper()
        {
        }

        private ServiceClientWrapper(
            string connectionString,
            TransportType transportType,
            ILogger logger)
        {
            this.ConnectionString = connectionString;
            this.TransportType =transportType;
            this.Logger = logger;

            this.serviceClient = ServiceClient.CreateFromConnectionString(
                this.ConnectionString,
                this.TransportType);
        }

        public async Task CloseClientAsync()
        {
            await this.serviceClient.CloseAsync();
        }

        public static ServiceClientWrapper Create(
            string connectionString,
            TransportType transportType,
            ILogger logger)
        {
            return new ServiceClientWrapper(
                connectionString,
                transportType,
                logger);
        }

        public async Task<HttpStatusCode> InvokeDirectMethodAsync(CancellationTokenSource cts)
        {
            ILogger logger = this.Logger;
            logger.LogInformation("Invoke DirectMethod from cloud: started.");

            string deviceId = Settings.Current.DeviceId;
            string targetModuleId = Settings.Current.TargetModuleId;

            logger.LogInformation($"Calling Direct Method from cloud on device {deviceId} targeting module [{targetModuleId}] with count {this.directMethodCount}.");

            try
            {
                CloudToDeviceMethod cloudToDeviceMethod = new CloudToDeviceMethod("HelloWorldMethod").SetPayloadJson("{ \"Message\": \"Hello\" }");
                CloudToDeviceMethodResult result = await this.serviceClient.InvokeDeviceMethodAsync(deviceId, targetModuleId, cloudToDeviceMethod, CancellationToken.None);

                string statusMessage = $"Calling Direct Method from cloud with count {this.directMethodCount} returned with status code {result.Status}";
                if (result.Status == (int)HttpStatusCode.OK)
                {
                    logger.LogDebug(statusMessage);
                }
                else
                {
                    logger.LogError(statusMessage);
                }

                this.directMethodCount++;
                logger.LogInformation("Invoke DirectMethod from cloud: finished.");
                return (HttpStatusCode)result.Status;
            }
            catch (Exception e)
            {
                logger.LogError($"Exception caught with count {this.directMethodCount}: {e}");
                return HttpStatusCode.InternalServerError;
            }
        }

        public async Task OpenClientAsync()
        {
            await this.serviceClient.OpenAsync();
        }

        public Task SendEventAsync(string outputName, string message)
        {
            return Task.CompletedTask;
        }
    }
}
