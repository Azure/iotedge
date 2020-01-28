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

        static long restartCount = 0;
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
                Dictionary<string, Task<Tuple<DateTime, HttpStatusCode>>> taskList = new Dictionary<string, Task<Tuple<DateTime, HttpStatusCode>>>();

                while ((!cts.IsCancellationRequested) && (DateTime.UtcNow < testExpirationTime))
                {
                    DateTime eachTestExpirationTime = testStart.AddMinutes(Settings.Current.RestartIntervalInMins);
                    (DateTime restartTime, HttpStatusCode restartStatus) = await RestartModules(iotHubServiceClient, cts);

                    // Increment the counter when issue an edgeHub restart
                    restartCount++;

                    // Secretly embedded the verification info in the Seq Number
                    // Last 44 bits are package seqeunce number while the first 20 bits are restart seqeunce.
                    Interlocked.Exchange(ref messageCount, restartCount << 44);
                    Interlocked.Exchange(ref directMethodCount, restartCount << 44);

                    // Setup Message Task
                    if (Settings.Current.MessageEnable)
                    {
                        Task<Tuple<DateTime, HttpStatusCode>> sendMessageTask = SendMessageAsync(
                            msgModuleClient,
                            Settings.Current.TrackingId,
                            batchId,
                            Settings.Current.MessageOutputEndpoint,
                            eachTestExpirationTime,
                            cts);

                        taskList.Add(
                            TestOperationResultType.Messages.ToString(),
                            sendMessageTask);
                    }

                    // Setup Direct Method Task
                    if (Settings.Current.DirectMethodEnable)
                    {
                        Task<Tuple<DateTime, HttpStatusCode>> sendDirectMethodTask = SendDirectMethodAsync(
                            Settings.Current.DeviceId,
                            Settings.Current.DirectMethodTargetModuleId,
                            dmModuleClient,
                            Settings.Current.DirectMethodName,
                            testExpirationTime,
                            cts);

                        taskList.Add(
                            TestOperationResultType.DirectMethod.ToString(),
                            sendDirectMethodTask);
                    }

                    // Each task gets its own thread from a threadpool
                    List<Task<Tuple<DateTime, HttpStatusCode>>> taskWaitlist = new List<Task<Tuple<DateTime, HttpStatusCode>>>();
                    foreach (var eachTaskEntry in taskList)
                    {
                        eachTaskEntry.Value.Start();
                        taskWaitlist.Add(eachTaskEntry.Value);
                    }

                    // Wait for treads to be done
                    Task.WaitAll(taskWaitlist.ToArray());

                    // Get the result and report it to TRC
                    if (Settings.Current.MessageEnable)
                    {
                        (DateTime msgCompletedTime, HttpStatusCode msgStatusCode) = taskList[TestOperationResultType.Messages.ToString()].Result;

                        TestResultBase msgTestResult = CreateTestResult(
                        TestOperationResultType.Messages,
                        msgCompletedTime,
                        msgStatusCode,
                        batchId.ToString(),
                        Interlocked.Read(ref messageCount));

                        await ModuleUtil.ReportTestResultAsync(reportClient, Logger, msgTestResult);
                    }

                    if (Settings.Current.DirectMethodEnable)
                    {
                        (DateTime dmCompletedTime, HttpStatusCode dmStatusCode) = taskList[TestOperationResultType.DirectMethod.ToString()].Result;

                        TestResultBase dmTestResult = CreateTestResult(
                        TestOperationResultType.DirectMethod,
                        restartTime,
                        restartStatus,
                        dmCompletedTime,
                        dmStatusCode,
                        batchId,
                        Interlocked.Read(ref directMethodCount));
                        // BEARWASHERE -- Add restart sequence number to the two reporting type

                        await ModuleUtil.ReportTestResultAsync(reportClient, Logger, dmTestResult);
                    }

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

        static TestResultBase CreateTestResult(
            TestOperationResultType testOperationResultType,
            DateTime edgeHubRestartedTime,
            HttpStatusCode edgeHubRestartStatusCode,
            DateTime completedTime,
            HttpStatusCode completedStatus,
            Guid batchId,
            long sequenceNumber)
        {
            switch (testOperationResultType)
            {
                // BEARWASHERE -- Create the two new types to send the report for DM & MSG
                case TestOperationResultType.Messages:
                    return new EdgeHubRestartMessageResult(
                        Settings.Current.ModuleId + testOperationResultType.ToString(),
                        DateTime.UtcNow,
                        Settings.Current.TrackingId,
                        batchId.ToString(),
                        sequenceNumber.ToString(),
                        edgeHubRestartedTime,
                        edgeHubRestartStatusCode,
                        completedTime,
                        completedStatus);

                case TestOperationResultType.DirectMethod:
                    return new EdgeHubRestartDirectMethodResult(
                        Settings.Current.ModuleId + testOperationResultType.ToString(),
                        DateTime.UtcNow,
                        Settings.Current.TrackingId,
                        batchId,
                        sequenceNumber.ToString(),
                        edgeHubRestartedTime,
                        edgeHubRestartStatusCode,
                        completedTime,
                        completedStatus);

                default:
                    throw new NotSupportedException($"{testOperationResultType} is not supported in CreateEdgeHubRestartTestResult()");
            }
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
