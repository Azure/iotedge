// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class DirectMethodEdgeHubConnectorTest : IEdgeHubConnector
    {
        readonly Guid batchId;
        readonly ILogger logger;
        long directMethodCount = 0;
        ModuleClient dmModuleClient = null;
        TestResultReportingClient reportClient = null;

        public DirectMethodEdgeHubConnectorTest(
            Guid batchId,
            ILogger logger)
        {
            this.batchId = batchId;
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        public void Dispose() => this.dmModuleClient?.Dispose();

        public async Task StartAsync(
            DateTime runExpirationTime,
            DateTime edgeHubRestartedTime,
            CancellationToken cancellationToken)
        {
            (DateTime dmCompletedTime, HttpStatusCode dmStatusCode) = await this.SendDirectMethodAsync(
                Settings.Current.DeviceId,
                Settings.Current.DirectMethodTargetModuleId,
                await this.GetModuleClientAsync(),
                Settings.Current.DirectMethodName,
                runExpirationTime,
                cancellationToken,
                this.logger);

            TestResultBase dmTestResult = new EdgeHubRestartDirectMethodResult(
                this.GetSourceString(),
                DateTime.UtcNow,
                Settings.Current.TrackingId,
                this.batchId,
                this.directMethodCount.ToString(),
                edgeHubRestartedTime,
                dmCompletedTime,
                dmStatusCode);

            await ModuleUtil.ReportTestResultAsync(
                this.GetReportClient(),
                this.logger,
                dmTestResult,
                cancellationToken);
        }

        async Task<Tuple<DateTime, HttpStatusCode>> SendDirectMethodAsync(
            string deviceId,
            string targetModuleId,
            ModuleClient moduleClient,
            string directMethodName,
            DateTime runExpirationTime,
            CancellationToken cancellationToken,
            ILogger logger)
        {
            while ((!cancellationToken.IsCancellationRequested) && (DateTime.UtcNow < runExpirationTime))
            {
                try
                {
                    // Direct Method sequence number is always increasing regardless of sending result.
                    this.directMethodCount++;
                    MethodRequest request = new MethodRequest(
                        directMethodName,
                        Encoding.UTF8.GetBytes($"{{ \"Message\": \"Hello\", \"DirectMethodCount\": \"{this.directMethodCount.ToString()}\" }}"),
                        Settings.Current.SdkOperationTimeout,
                        Settings.Current.SdkOperationTimeout);
                    MethodResponse result = await moduleClient.InvokeMethodAsync(deviceId, targetModuleId, request);
                    logger.LogInformation($"[DirectMethodEdgeHubConnector] Invoke DirectMethod with count {this.directMethodCount.ToString()}");

                    if ((HttpStatusCode)result.Status == HttpStatusCode.OK)
                    {
                        logger.LogDebug(result.ResultAsJson);
                    }
                    else
                    {
                        logger.LogError(result.ResultAsJson);
                    }

                    return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, (HttpStatusCode)result.Status);
                }
                catch (Exception e)
                {
                    // Only handle the exception that relevant to our test case; otherwise, re-throw it.
                    if (this.IsEdgeHubDownDuringDirectMethodSend(e) || this.IsDirectMethodReceiverNotConnected(e))
                    {
                        // swallow exeception and retry until success
                        logger.LogDebug(e, $"[DirectMethodEdgeHubConnector] Exception caught with SequenceNumber {this.directMethodCount.ToString()}");
                    }
                    else
                    {
                        // something is wrong, Log and send report to TRC
                        string errorMessage = $"[DirectMethodEdgeHubConnector] Exception caught with SequenceNumber {this.directMethodCount.ToString()}";
                        logger.LogError(e, errorMessage);

                        TestResultBase errorResult = new ErrorTestResult(
                            Settings.Current.TrackingId,
                            this.GetSourceString(),
                            errorMessage,
                            DateTime.UtcNow);

                        await ModuleUtil.ReportTestResultAsync(
                            this.GetReportClient(),
                            this.logger,
                            errorResult,
                            cancellationToken);
                    }
                }
            }

            return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.InternalServerError);
        }

        async Task<ModuleClient> GetModuleClientAsync()
        {
            if (this.dmModuleClient == null)
            {
                this.dmModuleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    this.logger);
            }

            return this.dmModuleClient;
        }

        TestResultReportingClient GetReportClient()
        {
            if (this.reportClient == null)
            {
                this.reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
            }

            return this.reportClient;
        }

        string GetSourceString() => Settings.Current.ModuleId + "." + TestOperationResultType.EdgeHubRestartDirectMethod.ToString();

        bool IsEdgeHubDownDuringDirectMethodSend(Exception e)
        {
            // This is a socket exception error code when EdgeHub is down.
            const int EdgeHubNotAvailableErrorCode = 111;

            if (e is IotHubCommunicationException)
            {
                if (e?.InnerException?.InnerException is SocketException)
                {
                    int errorCode = ((SocketException)e.InnerException.InnerException).ErrorCode;
                    return errorCode == EdgeHubNotAvailableErrorCode;
                }
            }

            return false;
        }

        bool IsDirectMethodReceiverNotConnected(Exception e)
        {
            if (e is DeviceNotFoundException)
            {
                string errorMsg = e.Message;
                return Regex.IsMatch(errorMsg, $"\\b{Settings.Current.DirectMethodTargetModuleId}\\b");
            }

            return false;
        }
    }
}