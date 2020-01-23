// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("EdgeHubRestartTester");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Logger.LogInformation($"Starting module restarter with the following settings:\r\n{Settings.Current}");

            ModuleClient moduleClient = null;
            try
            {
                TestResultReportingClient reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };


                moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.MessageTransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);


                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                while (!cts.Token.IsCancellationRequested)
                {
                    (DateTime restartTime, HttpStatusCode restartStatus) = await RestartModules(iotHubServiceClient, cts);

                    // BEARWASHERE -- Send DM until it passes, task it
                    // BEARWASHERE -- Send Msg until it passes, task it
                    // BEARWASHERE -- Send the "pass" response

                    // BEARWASHERE -- FIgureout how to much delay I should have.
                    await Task.Delay(Settings.Current.RestartIntervalInMins * 60 * 1000, cts.Token);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception caught: {e}");
                throw;
            }
            finally
            {
                moduleClient?.Dispose();
            }

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("EdgeHubRestartTester Main() finished.");
            return 0;
        }

        static void SendMessage(ModuleClient moduleClient)
        {
            // BEARWASHERE -- TODO
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
        }
    }
}
