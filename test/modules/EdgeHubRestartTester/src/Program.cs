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
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Message = Microsoft.Azure.Devices.Client.Message;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("EdgeHubRestartTester");
        static long messageCount = 0;
        const string outputEndpoint = "output1";

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Guid batchId = Guid.NewGuid();
            Logger.LogInformation($"Starting Edge Hub Restart Tester ({batchId}) with the following settings:\r\n{Settings.Current}");
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            ModuleClient moduleClient = null;
            try
            {
                ServiceClient iotHubServiceClient = ServiceClient.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString);
                TestResultReportingClient reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
                DateTime testExpirationTime = DateTime.UtcNow.AddMinutes(Settings.Current.RestartIntervalInMins);

                moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.MessageTransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);
                Task<DateTime> SendMessageTask = SendMessageAsync(
                    moduleClient,
                    Settings.Current.TrackingId,
                    batchId,
                    testExpirationTime,
                    cts);

                while ((!cts.IsCancellationRequested) && (DateTime.UtcNow < testExpirationTime))
                {
                    (DateTime restartTime, HttpStatusCode restartStatus) = await RestartModules(iotHubServiceClient, cts);

                    // BEARWASHERE -- Send DM until it passes, task it
                    // BEARWASHERE -- Send Msg until it passes, task it

                    // BEARWASHERE -- Send the "pass" response
                    SendMessageTask.Start();

                    // BEARWASHERE -- FIgureout how to much delay I should have.
                    Task.WaitAll(SendMessageTask);

                    DateTime msgCompletedTime = SendMessageTask.Result;

                    DateTime.UtcNow - testStart;
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

        static async Task<DateTime> SendMessageAsync(
            ModuleClient moduleClient,
            string trackingId,
            Guid batchId,
            DateTime testExpirationTime,
            CancellationTokenSource cts)
        {
            while ((!cts.Token.IsCancellationRequested) && (DateTime.UtcNow < testExpirationTime))
            {
                // BEARWASHERE -- TODO: Test this
                Message message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {data = DateTime.UtcNow.ToString()})));
                Interlocked.Increment(ref messageCount);
                message.Properties.Add("sequenceNumber", Interlocked.Read(ref messageCount).ToString());
                message.Properties.Add("batchId", batchId.ToString());
                message.Properties.Add("trackingId", trackingId);

                try
                {
                    // sending the result via edgeHub
                    await moduleClient.SendEventAsync(outputEndpoint, message);
                    return DateTime.UtcNow;
                }
                catch (OperationCanceledException ex)
                {
                    Logger.LogError(ex, $"[SendEventAsync] Sequence number {messageCount}, BatchId: {batchId.ToString()};");
                    Interlocked.Decrement(ref messageCount);
                }
            }
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
            return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, (HttpStatusCode)HttpStatusCode.InternalServerError);
        }
    }
}
