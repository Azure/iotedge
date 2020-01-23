// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
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
            Guid batchId = Guid.NewGuid();
            Logger.LogInformation($"Starting Edge Hub Restart Tester ({batchId}) with the following settings:\r\n{Settings.Current}");

            Logger.LogInformation($"Load gen delay start for {Settings.Current.TestStartDelay}.");
            await Task.Delay(Settings.Current.TestStartDelay);

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            ServiceClient iotHubServiceClient = null;
            ModuleClient msgModuleClient = null;
            ModuleClient dmModuleClient = null;
            try
            {
                iotHubServiceClient = ServiceClient.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString);
                TestResultReportingClient reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };

                msgModuleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.MessageTransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);

                dmModuleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.MessageTransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);

                DateTime testStart = DateTime.UtcNow;
                DateTime testExpirationTime = testStart + Settings.Current.TestDuration;
                while ((!cts.IsCancellationRequested) && (DateTime.UtcNow < testExpirationTime))
                {
                    DateTime eachTestExpirationTime = testStart.AddMinutes(Settings.Current.RestartIntervalInMins);
                    (DateTime restartTime, HttpStatusCode restartStatus) = await RestartModules(iotHubServiceClient, cts);

                    // Setup Direct Method Task
                    Task<Tuple<DateTime, HttpStatusCode>> sendDirectMethodTask = SendDirectMethodAsync(
                        Settings.Current.DeviceId,
                        Settings.Current.DirectMethodTargetModuleId,
                        dmModuleClient,
                        Settings.Current.DirectMethodName,
                        testExpirationTime,
                        cts);

                    // Setup Message Task
                    Task<Tuple<DateTime, HttpStatusCode>> sendMessageTask = SendMessageAsync(
                        msgModuleClient,
                        Settings.Current.TrackingId,
                        batchId,
                        Settings.Current.MessageOutputEndpoint,
                        eachTestExpirationTime,
                        cts);

                    // Each task gets its own thread from a threadpool
                    sendDirectMethodTask.Start();
                    sendMessageTask.Start();

                    // Wait for treads to be done
                    Task.WaitAll(sendDirectMethodTask, sendMessageTask);

                    // Get the result and report it to TRC
                    (DateTime msgCompletedTime, HttpStatusCode msgStatusCode) = sendMessageTask.Result;
                    (DateTime dmCompletedTime, HttpStatusCode dmStatusCode) = sendDirectMethodTask.Result;

                    // Generate reports, the new report type
                    EdgeHubRestartTestResult msgTestResult = CreateEdgeHubRestartTestResult(
                        TestOperationResultType.Messages,
                        restartTime,
                        msgCompletedTime,
                        restartStatus,
                        msgStatusCode,
                        batchId.ToString(),
                        Interlocked.Read(ref messageCount));

                    EdgeHubRestartTestResult dmTestResult = CreateEdgeHubRestartTestResult(
                        TestOperationResultType.DirectMethod,
                        restartTime,
                        dmCompletedTime,
                        restartStatus,
                        dmStatusCode,
                        batchId.ToString(),
                        Interlocked.Read(ref directMethodCount),
                        Settings.Current.TrackingId);

                    // Send results to TRC
                    await ModuleUtil.ReportTestResultAsync(reportClient, Logger, msgTestResult);
                    await ModuleUtil.ReportTestResultAsync(reportClient, Logger, dmTestResult);

                    // BEARWASHERE -- TODO: update the TRC
                    //  1. Update the MetaData
                    //  2. Use the MetaData in the deployment

                    // Wait to do another restart
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

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("EdgeHubRestartTester Main() finished.");
            return 0;
        }

        static EdgeHubRestartTestResult CreateEdgeHubRestartTestResult(
            TestOperationResultType testOperationResultType,
            DateTime restartTime,
            DateTime completedTime,
            HttpStatusCode restartStatus,
            HttpStatusCode completedStatus,
            string batchId = "",
            long sequenceNumber = 0,
            string trackingId = "")
        {
            TestResultBase attachedTestResult = null;
            switch (testOperationResultType)
            {
                case TestOperationResultType.Messages:
                    attachedTestResult = new MessageTestResult(
                        Settings.Current.ModuleId + ".send",
                        completedTime)
                    {
                        TrackingId = Settings.Current.TrackingId,
                        BatchId = batchId,
                        SequenceNumber = sequenceNumber.ToString()
                    };
                    break;

                case TestOperationResultType.DirectMethod:
                    attachedTestResult = new DirectMethodTestResult(
                        Settings.Current.ModuleId + ".send",
                        completedTime,
                        trackingId,
                        Guid.Parse(batchId),
                        sequenceNumber.ToString(),
                        completedStatus);
                    break;

                default:
                    throw new NotSupportedException($"{testOperationResultType} is not supported in CreateEdgeHubRestartTestResult()");
            }

            return new EdgeHubRestartTestResult(
                Settings.Current.ModuleId + testOperationResultType.ToString(),
                testOperationResultType,
                DateTime.UtcNow)
            {
                TestResult = attachedTestResult,
                EdgeHubRestartTime = restartTime,
                EdgeHubUplinkTime = completedTime,
                RestartHttpStatusCode = restartStatus,
                UplinkHttpStatusCode = completedStatus
            };
        }

        static async Task<Tuple<DateTime, HttpStatusCode>> SendDirectMethodAsync(
            string deviceId,
            string targetModuleId,
            ModuleClient moduleClient,
            string directMethodName,
            DateTime testExpirationTime,
            CancellationTokenSource cts)
        {
            while ((!cts.Token.IsCancellationRequested) && (DateTime.UtcNow < testExpirationTime))
            {
                // BEARWASHERE -- TODO: Test this
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

                    Logger.LogInformation($"Invoke DirectMethod with count {Interlocked.Read(ref directMethodCount).ToString()}: finished.");
                    return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, (HttpStatusCode)result.Status);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, $"Exception caught with count {Interlocked.Read(ref directMethodCount).ToString()}");
                }
            }

            return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.InternalServerError);
        }

        static async Task<Tuple<DateTime, HttpStatusCode>> SendMessageAsync(
            ModuleClient moduleClient,
            string trackingId,
            Guid batchId,
            string msgOutputEndpoint,
            DateTime testExpirationTime,
            CancellationTokenSource cts)
        {
            while ((!cts.Token.IsCancellationRequested) && (DateTime.UtcNow < testExpirationTime))
            {
                // BEARWASHERE -- TODO: Test this
                Message message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { data = DateTime.UtcNow.ToString() })));
                Interlocked.Increment(ref messageCount);
                message.Properties.Add("sequenceNumber", Interlocked.Read(ref messageCount).ToString());
                message.Properties.Add("batchId", batchId.ToString());
                message.Properties.Add("trackingId", trackingId);

                try
                {
                    // Sending the result via edgeHub
                    await moduleClient.SendEventAsync(msgOutputEndpoint, message);
                    return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.OK);
                }
                catch (OperationCanceledException ex)
                {
                    // The message sequence number is not incrementing if the send failed.
                    Logger.LogError(ex, $"[SendEventAsync] Sequence number {messageCount}, BatchId: {batchId.ToString()};");
                    Interlocked.Decrement(ref messageCount);
                }
            }

            return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.InternalServerError);
        }

        static async Task<Tuple<DateTime, HttpStatusCode>> RestartModules(ServiceClient iotHubServiceClient, CancellationTokenSource cts)
        {
            CloudToDeviceMethod c2dMethod = new CloudToDeviceMethod("RestartModule");
            string payloadSchema = "{{ \"SchemaVersion\": \"1.0\", \"Id\": \"{0}\" }}";
            string payload = string.Format(payloadSchema, "$edgeHub");
            Logger.LogInformation("RestartModule Method Payload: {0}", payload);
            c2dMethod.SetPayloadJson(payload);

            try
            {
                CloudToDeviceMethodResult response = await iotHubServiceClient.InvokeDeviceMethodAsync(Settings.Current.DeviceId, "$edgeAgent", c2dMethod);
                if ((HttpStatusCode)response.Status != HttpStatusCode.OK)
                {
                    Logger.LogError($"Calling Direct Method failed with status code {response.Status}.");
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
