// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    abstract class DirectMethodSenderBase : IDisposable
    {
        readonly ILogger logger;
        readonly string deviceId;
        readonly string targetModuleId;
        ulong directMethodCount = 0;

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

        public async Task<Tuple<HttpStatusCode, ulong>> InvokeDirectMethodAsync(string methodName, CancellationTokenSource cts)
        {
            ILogger logger = this.logger;
            logger.LogDebug("Invoke DirectMethod: started.");

            this.directMethodCount++;
            logger.LogInformation($"{this.GetType().ToString()} : Calling Direct Method on device {this.deviceId} targeting module [{this.targetModuleId}] with count {this.directMethodCount}.");
            try
            {
                int resultStatus = await this.InvokeDirectMethodWithRetryAsync(logger, this.deviceId, this.targetModuleId, methodName, this.directMethodCount);

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
                return new Tuple<HttpStatusCode, ulong>((HttpStatusCode)resultStatus, this.directMethodCount);
            }
            catch (DeviceNotFoundException e)
            {
                logger.LogInformation(e, $"DeviceNotFound exception caught with count {this.directMethodCount}");
                return new Tuple<HttpStatusCode, ulong>(HttpStatusCode.NotFound, this.directMethodCount);
            }
            catch (SocketException e)
            {
                logger.LogInformation(e, $"Resource exception caught with count {this.directMethodCount}");
                return new Tuple<HttpStatusCode, ulong>(HttpStatusCode.ServiceUnavailable, this.directMethodCount);
            }
            catch (UnauthorizedException e)
            {
                logger.LogInformation(e, $"Unauthorized exception caught with count {this.directMethodCount}");
                return new Tuple<HttpStatusCode, ulong>(HttpStatusCode.Unauthorized, this.directMethodCount);
            }
            catch (Exception e) when (e is System.Net.Http.HttpRequestException || e is IotHubException || e is IotHubCommunicationException)
            {
                logger.LogInformation(e, $"Transient exception caught with count {this.directMethodCount}");
                return new Tuple<HttpStatusCode, ulong>(HttpStatusCode.FailedDependency, this.directMethodCount);
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Exception caught with count {this.directMethodCount}");
                return new Tuple<HttpStatusCode, ulong>(HttpStatusCode.InternalServerError, this.directMethodCount);
            }
        }

        // Retry is needed here because sometimes the test agents have transient
        // network issues. Ideally, we would just send the report to the TRC and
        // have it analyze whether it is a pass or fail. However the transient
        // exceptions don't have status codes which adds complication. This retry
        // approach is easier.
        async Task<int> InvokeDirectMethodWithRetryAsync(
            ILogger logger,
            string deviceId,
            string targetModuleId,
            string methodName,
            ulong directMethodCount)
        {
            const int maxRetry = 3;
            int resultStatus = 0;
            int transientRetryCount = 0;

            while (transientRetryCount < maxRetry)
            {
                try
                {
                    logger.LogDebug($"InvokeDirectMethodWithRetryAsync with count {directMethodCount}; Retry {transientRetryCount}");
                    transientRetryCount++;
                    resultStatus = await this.InvokeDeviceMethodAsync(deviceId, targetModuleId, methodName, directMethodCount, CancellationToken.None);
                    break;
                }
                catch (IotHubCommunicationException e) when (e.IsTransient)
                {
                    logger.LogInformation(e, $"Transient IotHubCommunicationException caught with count {directMethodCount}. Retry: {transientRetryCount}");
                    resultStatus = (int)HttpStatusCode.RequestTimeout;
                }
            }

            return resultStatus;
        }

        internal abstract Task<int> InvokeDeviceMethodAsync(
            string deviceId,
            string targetModuleId,
            string methodName,
            ulong directMethodCount,
            CancellationToken none);
    }
}
