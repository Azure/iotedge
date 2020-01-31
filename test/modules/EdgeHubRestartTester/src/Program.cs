// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Message = Microsoft.Azure.Devices.Client.Message;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("EdgeHubRestartTester");

        static long messageCount = 0;
        static long directMethodCount = 0;

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            uint restartCount = 0;

            Guid batchId = Guid.NewGuid();
            Logger.LogInformation($"Starting EdgeHubRestartTester ({batchId}) with the following settings:\r\n{Settings.Current}");

            Logger.LogInformation($"EdgeHubRestartTester delay start for {Settings.Current.TestStartDelay}.");
            await Task.Delay(Settings.Current.TestStartDelay);

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(Settings.Current.RestartPeriod - TimeSpan.FromMinutes(1), Logger);

            ServiceClient iotHubServiceClient = null;
            ModuleClient msgModuleClient = null;
            ModuleClient dmModuleClient = null;

            try
            {
                iotHubServiceClient = ServiceClient.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString);

                if (Settings.Current.MessageEnable)
                {
                    msgModuleClient = await ModuleUtil.CreateModuleClientAsync(
                        Settings.Current.MessageTransportType,
                        ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                        ModuleUtil.DefaultTransientRetryStrategy,
                        Logger);
                    msgModuleClient.OperationTimeoutInMilliseconds = Settings.Current.SdkRetryTimeout;
                }

                if (Settings.Current.DirectMethodEnable)
                {
                    dmModuleClient = await ModuleUtil.CreateModuleClientAsync(
                        Settings.Current.MessageTransportType,
                        ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                        ModuleUtil.DefaultTransientRetryStrategy,
                        Logger);
                }

                DateTime testStart = DateTime.UtcNow;
                DateTime testExpirationTime = testStart + Settings.Current.TestDuration;

                while ((!cts.IsCancellationRequested) && (DateTime.UtcNow < testExpirationTime))
                {
                    (DateTime restartTime, HttpStatusCode restartStatus) = await RestartModules(iotHubServiceClient);
                    DateTime eachTestExpirationTime = restartTime.Add(Settings.Current.RestartPeriod);

                    // Increment the counter when issue an edgeHub restart
                    restartCount++;

                    // Setup Message Task
                    Task sendMessageTask = Task.CompletedTask;

                    try
                    {
                        if (Settings.Current.MessageEnable)
                        {
                            Func<Task> sendMessage =
                                async () =>
                                {
                                    (DateTime msgCompletedTime, HttpStatusCode msgStatusCode) = await SendMessageAsync(
                                        msgModuleClient,
                                        Settings.Current.TrackingId,
                                        batchId,
                                        Settings.Current.MessageOutputEndpoint,
                                        eachTestExpirationTime,
                                        cts.Token).ConfigureAwait(false);
                                    TestResultBase msgTestResult = CreateTestResult(
                                        TestOperationResultType.EdgeHubRestartMessage,
                                        restartTime,
                                        restartStatus,
                                        msgCompletedTime,
                                        msgStatusCode,
                                        batchId,
                                        restartCount,
                                        Interlocked.Read(ref messageCount));
                                    var reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
                                    await ModuleUtil.ReportTestResultUntilSuccessAsync(
                                        reportClient,
                                        Logger,
                                        msgTestResult,
                                        cts.Token).ConfigureAwait(false);
                                };
                            sendMessageTask = sendMessage();
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        sendMessageTask = Task.CompletedTask;
                        Logger.LogError($"{nameof(sendMessageTask)} HttpRequestException: {ex}");
                    }

                    // Setup Direct Method Task
                    Task directMethodTask = Task.CompletedTask;

                    try
                    {
                        if (Settings.Current.DirectMethodEnable)
                        {
                            Func<Task> directMethod =
                                async () =>
                                {
                                    (DateTime dmCompletedTime, HttpStatusCode dmStatusCode) = await SendDirectMethodAsync(
                                        Settings.Current.DeviceId,
                                        Settings.Current.DirectMethodTargetModuleId,
                                        dmModuleClient,
                                        Settings.Current.DirectMethodName,
                                        eachTestExpirationTime,
                                        cts.Token).ConfigureAwait(false);
                                    TestResultBase dmTestResult = CreateTestResult(
                                        TestOperationResultType.EdgeHubRestartDirectMethod,
                                        restartTime,
                                        restartStatus,
                                        dmCompletedTime,
                                        dmStatusCode,
                                        batchId,
                                        restartCount,
                                        Interlocked.Read(ref directMethodCount));
                                    var reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
                                    await ModuleUtil.ReportTestResultUntilSuccessAsync(
                                        reportClient,
                                        Logger,
                                        dmTestResult,
                                        cts.Token).ConfigureAwait(false);
                                };
                            directMethodTask = directMethod();
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        directMethodTask = Task.CompletedTask;
                        Logger.LogError($"{nameof(directMethodTask)} HttpRequestException: {ex}");
                    }

                    // Wait for the two task to be done before do a restart
                    await Task.WhenAll(new[] { sendMessageTask, directMethodTask });

                    // Wait until the specified restart period to do another restart
                    await Task.Delay((int)(eachTestExpirationTime - DateTime.UtcNow).TotalMilliseconds, cts.Token);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception caught: {e}");
                throw;
            }
            finally
            {
                iotHubServiceClient?.Dispose();
                dmModuleClient?.Dispose();
                msgModuleClient?.Dispose();
            }

            await cts.Token.WhenCanceled();
            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("EdgeHubRestartTester Main() finished.");
            return 0;
        }

        static TestResultBase CreateTestResult(
            TestOperationResultType testOperationResultType,
            DateTime edgeHubRestartedTime,
            HttpStatusCode edgeHubRestartStatusCode,
            DateTime completedTime,
            HttpStatusCode completedStatus,
            Guid batchId,
            uint restartSequenceNumber,
            long sequenceNumber)
        {
            switch (testOperationResultType)
            {
                case TestOperationResultType.EdgeHubRestartMessage:
                    return new EdgeHubRestartMessageResult(
                        Settings.Current.ModuleId + "." + testOperationResultType.ToString(),
                        DateTime.UtcNow,
                        Settings.Current.TrackingId,
                        batchId.ToString(),
                        sequenceNumber.ToString(),
                        edgeHubRestartedTime,
                        edgeHubRestartStatusCode,
                        completedTime,
                        completedStatus,
                        restartSequenceNumber);

                case TestOperationResultType.EdgeHubRestartDirectMethod:
                    return new EdgeHubRestartDirectMethodResult(
                        Settings.Current.ModuleId + "." + testOperationResultType.ToString(),
                        DateTime.UtcNow,
                        Settings.Current.TrackingId,
                        batchId,
                        sequenceNumber.ToString(),
                        edgeHubRestartedTime,
                        edgeHubRestartStatusCode,
                        completedTime,
                        completedStatus,
                        restartSequenceNumber);

                default:
                    throw new NotSupportedException($"{testOperationResultType} is not supported in CreateEdgeHubRestartTestResult()");
            }
        }

        static async Task<Tuple<DateTime, HttpStatusCode>> SendDirectMethodAsync(
            string deviceId,
            string targetModuleId,
            ModuleClient moduleClient,
            string directMethodName,
            DateTime runExpirationTime,
            CancellationToken cancellationToken)
        {
            while ((!cancellationToken.IsCancellationRequested) && (DateTime.UtcNow < runExpirationTime))
            {
                try
                {
                    // Direct Method sequence number is always increasing regardless of sending result.
                    Interlocked.Increment(ref directMethodCount);
                    MethodRequest request = new MethodRequest(
                        directMethodName,
                        Encoding.UTF8.GetBytes($"{{ \"Message\": \"Hello\", \"DirectMethodCount\": \"{Interlocked.Read(ref directMethodCount).ToString()}\" }}"));
                    MethodResponse result = await moduleClient.InvokeMethodAsync(deviceId, targetModuleId, request);
                    if ((HttpStatusCode)result.Status == HttpStatusCode.OK)
                    {
                        Logger.LogDebug(result.ResultAsJson);
                    }
                    else
                    {
                        Logger.LogError(result.ResultAsJson);
                    }

                    Logger.LogInformation($"[SendDirectMethodAsync] Invoke DirectMethod with count {Interlocked.Read(ref directMethodCount).ToString()}");
                    return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, (HttpStatusCode)result.Status);
                }
                catch (IotHubCommunicationException e)
                {
                    // Only handle the exception that relevant to our test case; otherwise, re-throw it.
                    if (IsEdgeHubDownDuringDirectMethodSend(e))
                    {
                        // swallow exeception and retry until success
                        Logger.LogDebug(e, $"[SendDirectMethodAsync] Exception caught with SequenceNumber {Interlocked.Read(ref directMethodCount).ToString()}");
                    }
                    else
                    {
                        // something is wrong, Log and re-throw
                        Logger.LogError(e, $"[SendDirectMethodAsync] Exception caught with SequenceNumber {Interlocked.Read(ref directMethodCount).ToString()}");
                        throw;
                    }
                }
            }

            return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.InternalServerError);
        }

        static bool IsEdgeHubDownDuringDirectMethodSend(IotHubCommunicationException e)
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

        static async Task<Tuple<DateTime, HttpStatusCode>> SendMessageAsync(
            ModuleClient moduleClient,
            string trackingId,
            Guid batchId,
            string msgOutputEndpoint,
            DateTime runExpirationTime,
            CancellationToken cancellationToken)
        {
            while ((!cancellationToken.IsCancellationRequested) && (DateTime.UtcNow < runExpirationTime))
            {
                Message message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { data = DateTime.UtcNow.ToString() })));
                Interlocked.Increment(ref messageCount);
                message.Properties.Add("sequenceNumber", Interlocked.Read(ref messageCount).ToString());
                message.Properties.Add("batchId", batchId.ToString());
                message.Properties.Add("trackingId", trackingId);

                try
                {
                    // Sending the result via edgeHub
                    await moduleClient.SendEventAsync(msgOutputEndpoint, message);
                    Logger.LogInformation($"[SendMessageAsync] Send Message with count {Interlocked.Read(ref messageCount).ToString()}: finished.");
                    return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.OK);
                }
                catch (TimeoutException ex)
                {
                    // TimeoutException is expected to happen while the EdgeHub is down.
                    // Let's log the attempt and retry the message send until successful
                    Logger.LogDebug(ex, $"[SendMessageAsync] Exception caught with SequenceNumber {messageCount}, BatchId: {batchId.ToString()};");
                }
            }

            return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.InternalServerError);
        }

        static async Task<Tuple<DateTime, HttpStatusCode>> RestartModules(
            ServiceClient iotHubServiceClient)
        {
            CloudToDeviceMethod c2dMethod = new CloudToDeviceMethod("RestartModule");
            string payloadSchema = "{{ \"SchemaVersion\": \"1.0\", \"Id\": \"{0}\" }}";
            string payload = string.Format(payloadSchema, "edgeHub");
            Logger.LogInformation("RestartModule Method Payload: {0}", payload);
            c2dMethod.SetPayloadJson(payload);

            try
            {
                CloudToDeviceMethodResult response = await iotHubServiceClient.InvokeDeviceMethodAsync(Settings.Current.DeviceId, "$edgeAgent", c2dMethod);
                if ((HttpStatusCode)response.Status != HttpStatusCode.OK)
                {
                    Logger.LogError($"Calling EdgeHub restart failed with status code {response.Status} : {response.GetPayloadAsJson()}.");
                }
                else
                {
                    Logger.LogInformation($"Calling EdgeHub restart succeeded with status code {response.Status}.");
                }

                return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, (HttpStatusCode)response.Status);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception caught for payload {payload}: {e}");
            }

            return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.InternalServerError);
        }
    }
}
