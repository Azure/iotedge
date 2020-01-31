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

    class DirectMethodEdgeHubConnector : IEdgeHubConnector
    {
        long directMethodCount = 0;

        ModuleClient DmModuleClient { get; }
        Guid BatchId { get; }
        DateTime RunExpirationTime { get; }
        CancellationToken CancellationToken { get; }
        DateTime EdgeHubRestartedTime { get; }
        HttpStatusCode EdgeHubRestartStatusCode { get; }
        uint RestartSequenceNumber { get; }
        ILogger Logger { get; }

        public DirectMethodEdgeHubConnector(
            ModuleClient dmModuleClient,
            Guid batchId,
            DateTime runExpirationTime,
            CancellationToken cancellationToken,
            DateTime edgeHubRestartedTime,
            HttpStatusCode edgeHubRestartStatusCode,
            uint restartSequenceNumber,
            ILogger logger)
        {
            this.DmModuleClient = Preconditions.CheckNotNull(dmModuleClient, nameof(dmModuleClient));
            this.BatchId = batchId;
            this.RunExpirationTime = runExpirationTime;
            this.CancellationToken = Preconditions.CheckNotNull(cancellationToken, nameof(cancellationToken));
            this.EdgeHubRestartedTime = edgeHubRestartedTime;
            this.EdgeHubRestartStatusCode = edgeHubRestartStatusCode;
            this.RestartSequenceNumber = Preconditions.CheckNotNull(restartSequenceNumber, nameof(restartSequenceNumber));
            this.Logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        public async Task ConnectAsync()
        {
            (DateTime dmCompletedTime, HttpStatusCode dmStatusCode) = await this.SendDirectMethodAsync(
                Settings.Current.DeviceId,
                Settings.Current.DirectMethodTargetModuleId,
                this.DmModuleClient,
                Settings.Current.DirectMethodName,
                this.RunExpirationTime,
                this.CancellationToken,
                this.Logger).ConfigureAwait(false);

            TestResultBase dmTestResult = new EdgeHubRestartDirectMethodResult(
                Settings.Current.ModuleId + "." + TestOperationResultType.EdgeHubRestartDirectMethod.ToString(),
                DateTime.UtcNow,
                Settings.Current.TrackingId,
                this.BatchId,
                Interlocked.Read(ref this.directMethodCount).ToString(),
                this.EdgeHubRestartedTime,
                this.EdgeHubRestartStatusCode,
                dmCompletedTime,
                dmStatusCode,
                this.RestartSequenceNumber);

            var reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
            await ModuleUtil.ReportTestResultUntilSuccessAsync(
                reportClient,
                this.Logger,
                dmTestResult,
                this.CancellationToken).ConfigureAwait(false);
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
                    Interlocked.Increment(ref this.directMethodCount);
                    MethodRequest request = new MethodRequest(
                        directMethodName,
                        Encoding.UTF8.GetBytes($"{{ \"Message\": \"Hello\", \"DirectMethodCount\": \"{Interlocked.Read(ref this.directMethodCount).ToString()}\" }}"));
                    MethodResponse result = await moduleClient.InvokeMethodAsync(deviceId, targetModuleId, request);
                    if ((HttpStatusCode)result.Status == HttpStatusCode.OK)
                    {
                        logger.LogDebug(result.ResultAsJson);
                    }
                    else
                    {
                        logger.LogError(result.ResultAsJson);
                    }

                    logger.LogInformation($"[DirectMethodEdgeHubConnector] Invoke DirectMethod with count {Interlocked.Read(ref this.directMethodCount).ToString()}");
                    return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, (HttpStatusCode)result.Status);
                }
                catch (IotHubCommunicationException e)
                {
                    // Only handle the exception that relevant to our test case; otherwise, re-throw it.
                    if (this.IsEdgeHubDownDuringDirectMethodSend(e))
                    {
                        // swallow exeception and retry until success
                        logger.LogDebug(e, $"[DirectMethodEdgeHubConnector] Exception caught with SequenceNumber {Interlocked.Read(ref this.directMethodCount).ToString()}");
                    }
                    else
                    {
                        // something is wrong, Log and re-throw
                        logger.LogError(e, $"[DirectMethodEdgeHubConnector] Exception caught with SequenceNumber {Interlocked.Read(ref this.directMethodCount).ToString()}");
                        throw;
                    }
                }
                catch (DeviceNotFoundException e)
                {
                    logger.LogError("BEARWASHERE ----------------------- AggregateException");
                    logger.LogError($"BEARWASHERE -------------------------------- IsType: {(e is DeviceNotFoundException).ToString()}");
                    var isReg = Regex.IsMatch(e.Message, $"\\b{Settings.Current.DirectMethodTargetModuleId}\\b");
                    logger.LogError($"BEARWASHERE -------------------------------- Regex : {isReg.ToString()}");
                    if (this.IsDirectMethodReceiverNotConnected(e))
                    {
                        // swallow exeception and retry until success
                        logger.LogDebug(e, $"[DirectMethodEdgeHubConnector] Exception caught with SequenceNumber {Interlocked.Read(ref this.directMethodCount).ToString()}");
                    }
                    else
                    {
                        // something is wrong, Log and re-throw
                        logger.LogError(e, $"[DirectMethodEdgeHubConnector] Exception caught with SequenceNumber {Interlocked.Read(ref this.directMethodCount).ToString()}");
                        throw;
                    }
                }
            }

            return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.InternalServerError);
        }

        bool IsEdgeHubDownDuringDirectMethodSend(IotHubCommunicationException e)
        {
            // This is a socket exception error code when EdgeHub is down.
            const int EdgeHubNotAvailableErrorCode = 111;

            if (e?.InnerException?.InnerException is SocketException)
            {
                int errorCode = ((SocketException)e.InnerException.InnerException).ErrorCode;
                return errorCode == EdgeHubNotAvailableErrorCode;
            }

            return false;
        }

        bool IsDirectMethodReceiverNotConnected(DeviceNotFoundException e)
        {
            string errorMsg = e.Message;
            return Regex.IsMatch(errorMsg, $"\\b{Settings.Current.DirectMethodTargetModuleId}\\b");
        }
    }
}