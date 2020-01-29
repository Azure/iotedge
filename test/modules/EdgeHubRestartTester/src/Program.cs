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

        static uint restartCount = 0;
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
                    (DateTime restartTime, HttpStatusCode restartStatus) = await RestartModules(iotHubServiceClient);

                    // Increment the counter when issue an edgeHub restart
                    restartCount++;

                    // Secretly embedded the verification info in the Seq Number
                    // Last 44 bits are package seqeunce number while the first 20 bits are restart seqeunce.
                    Interlocked.Exchange(ref messageCount, restartCount << 44);
                    Interlocked.Exchange(ref directMethodCount, restartCount << 44);

                    // Setup Message Task
                    Task sendMessageTask;
                    if (Settings.Current.MessageEnable)
                    {
                        Func<Task> sendMessage = async () => {
                            (DateTime msgCompletedTime, HttpStatusCode msgStatusCode) = await SendMessageAsync(
                                msgModuleClient,
                                Settings.Current.TrackingId,
                                batchId,
                                Settings.Current.MessageOutputEndpoint,
                                eachTestExpirationTime,
                                cts.Token
                            ).ConfigureAwait(false);
                            TestResultBase msgTestResult = CreateTestResult(
                                TestOperationResultType.Messages,
                                restartTime,
                                restartStatus,
                                msgCompletedTime,
                                msgStatusCode,
                                batchId,
                                restartCount,
                                Interlocked.Read(ref messageCount)
                            );
                            var reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
                            await ModuleUtil.ReportTestResultAsync(
                                reportClient,
                                Logger,
                                msgTestResult,
                                cts.Token).ConfigureAwait(false);
                        };
                        sendMessageTask = sendMessage();
                    }
                    else
                    {
                        sendMessageTask = Task.CompletedTask;
                    }

                    // Setup Direct Method Task
                    Task directMethodTask;
                    if (Settings.Current.DirectMethodEnable)
                    {
                        Func<Task> directMethod = async () => {
                            (DateTime dmCompletedTime, HttpStatusCode dmStatusCode) = await SendDirectMethodAsync(
                                Settings.Current.DeviceId,
                                Settings.Current.DirectMethodTargetModuleId,
                                dmModuleClient,
                                Settings.Current.DirectMethodName,
                                testExpirationTime,
                                cts.Token).ConfigureAwait(false);
                            TestResultBase dmTestResult = CreateTestResult(
                                TestOperationResultType.DirectMethod,
                                restartTime,
                                restartStatus,
                                dmCompletedTime,
                                dmStatusCode,
                                batchId,
                                restartCount,
                                Interlocked.Read(ref directMethodCount));
                            var reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
                            await ModuleUtil.ReportTestResultAsync(
                                reportClient,
                                Logger,
                                dmTestResult,
                                cts.Token).ConfigureAwait(false);
                        };
                        directMethodTask = directMethod();
                    }
                    else
                    {
                        directMethodTask = Task.CompletedTask;
                    }

                    // Wait for the two task to be done before do a restart
                    await Task.WhenAll(new [] { sendMessageTask, directMethodTask });

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
                        completedStatus,
                        restartSequenceNumber);

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
            DateTime testExpirationTime,
            CancellationToken cancellationToken)
        {
            while ((!cancellationToken.IsCancellationRequested) && (DateTime.UtcNow < testExpirationTime))
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
            CancellationToken cancellationToken)
        {
            while ((!cancellationToken.IsCancellationRequested) && (DateTime.UtcNow < testExpirationTime))
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
                    Logger.LogError(ex, $"[SendEventAsync] Sequence number {messageCount}, BatchId: {batchId.ToString()};");
                }
            }

            return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.InternalServerError);
        }

        static async Task<Tuple<DateTime, HttpStatusCode>> RestartModules(
            ServiceClient iotHubServiceClient)
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
                    Logger.LogError($"Calling Direct Method failed with status code {response.Status} : {response.GetPayloadAsJson()} .");
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
