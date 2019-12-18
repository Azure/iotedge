// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public abstract class DirectMethodSenderBase
    {
        readonly ILogger logger;
        long directMethodCount = 1;

        protected DirectMethodSenderBase(ILogger logger)
        {
            this.logger = logger;
        }

        public abstract Task CloseAsync();

        public async Task<HttpStatusCode> InvokeDirectMethodAsync(CancellationTokenSource cts)
        {
            ILogger logger = this.logger;
            logger.LogInformation("Invoke DirectMethod from cloud: started.");

            string deviceId = Settings.Current.DeviceId;
            string targetModuleId = Settings.Current.TargetModuleId;

            logger.LogInformation($"Calling Direct Method from cloud on device {deviceId} targeting module [{targetModuleId}] with count {this.directMethodCount}.");

            try
            {
                int resultStatus = await this.InvokeDeviceMethodAsync(deviceId, targetModuleId, CancellationToken.None);

                string statusMessage = $"Calling Direct Method from cloud with count {this.directMethodCount} returned with status code {resultStatus}";
                if (resultStatus == (int)HttpStatusCode.OK)
                {
                    logger.LogDebug(statusMessage);
                }
                else
                {
                    logger.LogError(statusMessage);
                }

                this.directMethodCount++;
                logger.LogInformation("Invoke DirectMethod from cloud: finished.");
                return (HttpStatusCode)resultStatus;
            }
            catch (Exception e)
            {
                logger.LogError($"Exception caught with count {this.directMethodCount}: {e}");
                return HttpStatusCode.InternalServerError;
            }
        }

        internal abstract Task<int> InvokeDeviceMethodAsync(string deviceId, string targetModuleId, CancellationToken none);
    }
}
