// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("EdgeHubRestartTester");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Guid batchId = Guid.NewGuid();
            Logger.LogInformation($"Starting EdgeHubRestartTester ({batchId}) with the following settings:\r\n{Settings.Current}");

            Logger.LogInformation($"EdgeHubRestartTester delay start for {Settings.Current.TestStartDelay}.");
            await Task.Delay(Settings.Current.TestStartDelay);

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            ServiceClient iotHubServiceClient = null;
            IEdgeHubConnectorTest edgeHubMessageConnector = null;
            IEdgeHubConnectorTest edgeHubDirectMethodConnector = null;

            try
            {
                iotHubServiceClient = ServiceClient.CreateFromConnectionString(Settings.Current.IoTHubConnectionString);

                if (Settings.Current.MessageEnabled)
                {
                    edgeHubMessageConnector = new MessageEdgeHubConnectorTest(
                        batchId,
                        Logger);
                }

                if (Settings.Current.DirectMethodEnabled)
                {
                    edgeHubDirectMethodConnector = new DirectMethodEdgeHubConnectorTest(
                        batchId,
                        Logger);
                }

                DateTime testStart = DateTime.UtcNow;
                DateTime testExpirationTime = testStart + Settings.Current.TestDuration;

                while ((!cts.IsCancellationRequested) && (DateTime.UtcNow < testExpirationTime))
                {
                    DateTime restartTime = await RestartEdgeHubAsync(
                        iotHubServiceClient,
                        cts.Token);
                    DateTime eachTestExpirationTime = restartTime.Add(Settings.Current.RestartPeriod);

                    // Setup Message Task
                    Task sendMessageTask = Task.CompletedTask;
                    sendMessageTask = edgeHubMessageConnector?.StartAsync(
                        eachTestExpirationTime,
                        restartTime,
                        cts.Token);

                    // Setup Direct Method Task
                    Task directMethodTask = Task.CompletedTask;
                    directMethodTask = edgeHubDirectMethodConnector?.StartAsync(
                        eachTestExpirationTime,
                        restartTime,
                        cts.Token);

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
                edgeHubDirectMethodConnector?.Dispose();
                edgeHubMessageConnector?.Dispose();
            }

            await cts.Token.WhenCanceled();
            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("EdgeHubRestartTester Main() finished.");
            return 0;
        }

        static async Task<DateTime> RestartEdgeHubAsync(
            ServiceClient iotHubServiceClient,
            CancellationToken cancellationToken)
        {
            const uint maxRetry = 3;
            uint restartCount = 0;

            CloudToDeviceMethod c2dMethod = new CloudToDeviceMethod("RestartModule");
            string payloadSchema = "{{ \"SchemaVersion\": \"1.0\", \"Id\": \"{0}\" }}";
            string payload = string.Format(payloadSchema, "edgeHub");
            Logger.LogInformation("RestartModule Method Payload: {0}", payload);
            c2dMethod.SetPayloadJson(payload);

            while (restartCount < maxRetry)
            {
                try
                {
                    restartCount++;
                    // TODO: Introduce the offline scenario to use docker command.
                    CloudToDeviceMethodResult response = await iotHubServiceClient.InvokeDeviceMethodAsync(Settings.Current.DeviceId, "$edgeAgent", c2dMethod);
                    if ((HttpStatusCode)response.Status != HttpStatusCode.OK)
                    {
                        Logger.LogError($"Calling EdgeHub restart failed with status code {response.Status} : {response.GetPayloadAsJson()}.");
                    }
                    else
                    {
                        Logger.LogInformation($"Calling EdgeHub restart succeeded with status code {response.Status}.");
                    }

                    return DateTime.UtcNow;
                }
                catch (Exception e)
                {
                    Logger.LogError($"Exception caught for payload {payload}: {e}");

                    if (restartCount == maxRetry - 1)
                    {
                        string errorMessage = $"Failed to restart EdgeHub with payload: {payload}: {e}";
                        TestResultBase errorResult = new ErrorTestResult(
                            Settings.Current.TrackingId,
                            GetSource(),
                            errorMessage,
                            DateTime.UtcNow);

                        var reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
                        await ModuleUtil.ReportTestResultAsync(
                            reportClient,
                            Logger,
                            errorResult,
                            cancellationToken);

                        throw;
                    }
                }
            }

            return DateTime.UtcNow;
        }

        static string GetSource() => $"{Settings.Current.ModuleId}";
    }
}
