// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
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
            List<ModuleClient> moduleClients = new List<ModuleClient>();
            try
            {
                iotHubServiceClient = ServiceClient.CreateFromConnectionString(Settings.Current.IoTHubConnectionString);

                List<IEdgeHubConnectorTest> edgeHubConnectorTests = new List<IEdgeHubConnectorTest>();

                foreach (EdgeHubConnectorsConfig eachConfig in await Settings.Current.GetConnectorConfigAsync())
                {
                    if (eachConfig.MessageOutputEndpoint != null)
                    {
                        ModuleClient msgModuleClient = await ModuleUtil.CreateModuleClientAsync(
                            eachConfig.TransportType,
                            new ClientOptions(),
                            ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                            ModuleUtil.DefaultTransientRetryStrategy,
                            Logger);

                        msgModuleClient.OperationTimeoutInMilliseconds = (uint)Settings.Current.SdkOperationTimeout.TotalMilliseconds;

                        moduleClients.Add(msgModuleClient);
                        edgeHubConnectorTests.Add(
                            new MessageEdgeHubConnectorTest(
                                batchId,
                                Logger,
                                msgModuleClient,
                                eachConfig.MessageOutputEndpoint));
                    }

                    if (eachConfig.DirectMethodTargetModuleId != null)
                    {
                        ModuleClient dmModuleClient = await ModuleUtil.CreateModuleClientAsync(
                            eachConfig.TransportType,
                            new ClientOptions(),
                            ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                            ModuleUtil.DefaultTransientRetryStrategy,
                            Logger);

                        moduleClients.Add(dmModuleClient);
                        edgeHubConnectorTests.Add(
                            new DirectMethodEdgeHubConnectorTest(
                                batchId,
                                Logger,
                                dmModuleClient,
                                eachConfig.DirectMethodTargetModuleId));
                    }
                }

                DateTime testStart = DateTime.UtcNow;
                DateTime testCompletionTime = testStart + Settings.Current.TestDuration;

                while ((!cts.IsCancellationRequested) && (DateTime.UtcNow < testCompletionTime))
                {
                    DateTime restartTime = await RestartEdgeHubAsync(
                        iotHubServiceClient,
                        cts.Token);
                    DateTime eachTestExpirationTime = restartTime.Add(Settings.Current.RestartPeriod);

                    List<Task> taskList = new List<Task>();
                    foreach (IEdgeHubConnectorTest eachConnectorTest in edgeHubConnectorTests)
                    {
                        taskList.Add(
                            eachConnectorTest.StartAsync(
                                eachTestExpirationTime,
                                restartTime,
                                cts.Token));
                    }

                    // Wait for the two task to be done before do a restart
                    await Task.WhenAll(taskList);

                    // Wait until the specified restart period to do another restart
                    TimeSpan waitTime = eachTestExpirationTime - DateTime.UtcNow;
                    if (waitTime.TotalMilliseconds > 0)
                    {
                        await Task.Delay(waitTime, cts.Token);
                    }
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

                foreach (ModuleClient client in moduleClients)
                {
                    client.Dispose();
                }
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
            DateTime startAttemptTime = DateTime.UtcNow;

            CloudToDeviceMethod c2dMethod = new CloudToDeviceMethod("RestartModule");
            string payloadSchema = "{{ \"SchemaVersion\": \"1.0\", \"Id\": \"{0}\" }}";
            string payload = string.Format(payloadSchema, "edgeHub");
            Logger.LogInformation("RestartModule Method Payload: {0}", payload);
            c2dMethod.SetPayloadJson(payload);

            while (true)
            {
                try
                {
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

                    if (Settings.Current.RestartPeriod < DateTime.UtcNow - startAttemptTime)
                    {
                        string errorMessage = $"Failed to restart EdgeHub from {startAttemptTime} to {DateTime.UtcNow}:\n\n{e}\n\nPayload: {payload}";
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
        }

        static string GetSource() => $"{Settings.Current.ModuleId}";
    }
}
