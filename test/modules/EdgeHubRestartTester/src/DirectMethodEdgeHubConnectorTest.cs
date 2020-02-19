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

    class DirectMethodEdgeHubConnectorTest : IEdgeHubConnectorTest
    {
        readonly Guid batchId;
        readonly ILogger logger;
        long directMethodCount = 0;
        ModuleClient dmModuleClient = null;
        TestResultReportingClient reportClient = null;

        public DirectMethodEdgeHubConnectorTest(
            Guid batchId,
            ILogger logger,
            ModuleClient dmModuleClient)
        {
            this.batchId = batchId;
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.dmModuleClient = Preconditions.CheckNotNull(dmModuleClient, nameof(dmModuleClient));
        }

        public async Task StartAsync(
            DateTime runExpirationTime,
            DateTime edgeHubRestartedTime,
            CancellationToken cancellationToken)
        {
            (DateTime dmCompletedTime, HttpStatusCode dmStatusCode) = await this.SendDirectMethodAsync(
                Settings.Current.DeviceId,
                Settings.Current.DirectMethodTargetModuleId,
                Settings.Current.DirectMethodName,
                runExpirationTime,
                cancellationToken);

            TestResultBase dmTestResult = new EdgeHubRestartDirectMethodResult(
                this.GetSource(),
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
            string directMethodName,
            DateTime runExpirationTime,
            CancellationToken cancellationToken)
        {
            ModuleClient moduleClient = await this.GetModuleClientAsync();
            while ((!cancellationToken.IsCancellationRequested) && (DateTime.UtcNow < runExpirationTime))
            {
                try
                {
                    // Direct Method sequence number is always increasing regardless of sending result.
                    this.directMethodCount++;
                    MethodRequest request = new MethodRequest(
                        directMethodName,
                        Encoding.UTF8.GetBytes($"{{ \"Message\": \"Hello\", \"DirectMethodCount\": \"{this.directMethodCount}\" }}"),
                        TimeSpan.FromSeconds(5),   // Minimum value accepted by SDK
                        Settings.Current.SdkOperationTimeout);
                    MethodResponse result = await moduleClient.InvokeMethodAsync(deviceId, targetModuleId, request);
                    this.logger.LogInformation($"[DirectMethodEdgeHubConnector] Invoke DirectMethod with count {this.directMethodCount}");

                    if ((HttpStatusCode)result.Status == HttpStatusCode.OK)
                    {
                        this.logger.LogDebug(result.ResultAsJson);
                    }
                    else
                    {
                        this.logger.LogError(result.ResultAsJson);
                    }

                    return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, (HttpStatusCode)result.Status);
                }
                catch (Exception e)
                {
                    // Only handle the exception that relevant to our test case; otherwise, re-throw it.
                    if (this.IsEdgeHubDownDuringDirectMethodSend(e) || this.IsDirectMethodReceiverNotConnected(e))
                    {
                        // swallow exeception and retry until success
                        this.logger.LogDebug($"[DirectMethodEdgeHubConnector] Expected exception caught with SequenceNumber {this.directMethodCount}");
                    }
                    else
                    {
                        // something is wrong, Log and send report to TRC
                        string errorMessage = $"[DirectMethodEdgeHubConnector] Exception caught with SequenceNumber {this.directMethodCount}";
                        this.logger.LogError(e, errorMessage);

                        TestResultBase errorResult = new ErrorTestResult(
                            Settings.Current.TrackingId,
                            this.GetSource(),
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

        string GetSource() => $"{Settings.Current.ModuleId}.{TestOperationResultType.EdgeHubRestartDirectMethod.ToString()}";

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