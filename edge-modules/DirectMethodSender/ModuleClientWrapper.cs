// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    public class OpenModuleClientAsyncArgs : IOpenClientAsyncArgs
    {
        public readonly TransportType TransportType;
        public readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy;
        public readonly RetryStrategy RetryStrategy;
        public readonly ILogger Logger;

        public OpenModuleClientAsyncArgs(
            TransportType transportType,
            ITransientErrorDetectionStrategy transientErrorDetectionStrategy,
            RetryStrategy retryStrategy,
            ILogger logger)
        {
            this.TransportType = transportType;
            this.TransientErrorDetectionStrategy = transientErrorDetectionStrategy;
            this.RetryStrategy = retryStrategy;
            this.Logger = logger;
        }
    }

    public class ModuleClientWrapper : IDirectMethodClient
    {
        ModuleClient moduleClient = null;
        OpenModuleClientAsyncArgs initInfo = null;
        int directMethodCount = 1;

        public async Task CloseClientAsync()
        {
            Preconditions.CheckNotNull(this.moduleClient);
            await this.moduleClient.CloseAsync();
        }

        public async Task<HttpStatusCode> InvokeDirectMethodAsync(CancellationTokenSource cts)
        {
            ILogger logger = this.initInfo.Logger;
            logger.LogInformation("Invoke DirectMethod from module: started.");

            string deviceId = Settings.Current.DeviceId;
            string targetModuleId = Settings.Current.TargetModuleId;

            logger.LogInformation($"Calling Direct Method from module {deviceId} targeting module {targetModuleId}.");

            try
            {
                MethodRequest request = new MethodRequest("HelloWorldMethod", Encoding.UTF8.GetBytes("{ \"Message\": \"Hello\" }"));
                MethodResponse response = await this.moduleClient.InvokeMethodAsync(deviceId, targetModuleId, request);

                string statusMessage = $"Calling Direct Method from module with count {this.directMethodCount} returned with status code {response.Status}";
                if (response.Status == (int)HttpStatusCode.OK)
                {
                    logger.LogDebug(statusMessage);
                }
                else
                {
                    logger.LogError(statusMessage);
                }

                this.directMethodCount++;
                logger.LogInformation("Invoke DirectMethod from module: finished.");
                return (HttpStatusCode)response.Status;
            }
            catch (Exception e)
            {
                logger.LogError($"Exception caught with count {this.directMethodCount}: {e}");
                return HttpStatusCode.InternalServerError;
            }
        }

        public async Task OpenClientAsync(IOpenClientAsyncArgs args)
        {
            if (args == null)
            {
                throw new ArgumentException();
            }

            var info = args as OpenModuleClientAsyncArgs;

            // implicit OpenAsync()
            this.moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    info.TransportType,
                    info.TransientErrorDetectionStrategy,
                    info.RetryStrategy,
                    info.Logger);
            this.initInfo = info;
        }
    }
}
