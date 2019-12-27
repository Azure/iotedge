// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public abstract class DirectMethodSenderBase : IDisposable
    {
        readonly ILogger logger;
        readonly string deviceId;
        readonly string targetModuleId;
        long directMethodCount = 1;

        protected DirectMethodSenderBase(
            ILogger logger,
            string deviceId,
            string targetModuleId)
        {
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.targetModuleId = Preconditions.CheckNonWhiteSpace(targetModuleId, nameof(targetModuleId));
        }

        public abstract void Dispose();

        public async Task<HttpStatusCode> InvokeDirectMethodAsync(CancellationTokenSource cts)
        {
            ILogger logger = this.logger;
            logger.LogDebug("Invoke DirectMethod: started.");
            logger.LogInformation($"{this.GetType().ToString()} : Calling Direct Method on device {this.deviceId} targeting module [{this.targetModuleId}] with count {this.directMethodCount}.");

            try
            {
                int resultStatus = await this.InvokeDeviceMethodAsync(this.deviceId, this.targetModuleId, CancellationToken.None);

                string statusMessage = $"Calling Direct Method with count {this.directMethodCount} returned with status code {resultStatus}";
                if (resultStatus == (int)HttpStatusCode.OK)
                {
                    logger.LogDebug(statusMessage);
                }
                else
                {
                    logger.LogError(statusMessage);
                }

                logger.LogInformation($"Invoke DirectMethod with count {this.directMethodCount}: finished.");
                this.directMethodCount++;
                return (HttpStatusCode)resultStatus;
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Exception caught with count {this.directMethodCount}");
                return HttpStatusCode.InternalServerError;
            }
        }

        internal abstract Task<int> InvokeDeviceMethodAsync(string deviceId, string targetModuleId, CancellationToken none);
    }
}
