// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodCloudSender
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TransportType = Microsoft.Azure.Devices.TransportType;

    class Program
    {
        const string RouteOutputName = "output1";
        static readonly ILogger Logger = ModuleUtil.CreateLogger("DirectMethodCloudSender");

        public static int Main() => MainAsync().Result;

        public static async Task<int> MainAsync()
        {
            Logger.LogInformation($"Starting DirectMethodCloudSender with the following settings:\r\n{Settings.Current}");

            try
            {
                string serviceClientConnectionString = Settings.Current.ServiceClientConnectionString;
                Uri analyzerUrl = Settings.Current.AnalyzerUrl;

                ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(serviceClientConnectionString, (TransportType)Settings.Current.TransportType);
                AnalyzerClient analyzerClient = new AnalyzerClient { BaseUrl = analyzerUrl.AbsoluteUri };

                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                await CallDirectMethodFromCloud(serviceClient, Settings.Current.DirectMethodDelay, analyzerClient, cts);

                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
                Logger.LogInformation("DirectMethodCloudSender Main() finished.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error occurred during direct method cloud sender test setup.\r\n{ex}");
            }

            return 0;
        }

        static async Task CallDirectMethodFromCloud(
            ServiceClient serviceClient,
            TimeSpan delay,
            AnalyzerClient analyzerClient,
            CancellationTokenSource cts)
        {
            Logger.LogInformation("CallDirectMethodFromCloud started.");

            CloudToDeviceMethod cloudToDeviceMethod = new CloudToDeviceMethod("HelloWorldMethod").SetPayloadJson("{ \"Message\": \"Hello\" }");
            string deviceId = Settings.Current.DeviceId;
            string targetModuleId = Settings.Current.TargetModuleId;
            int directMethodCount = 1;

            while (!cts.Token.IsCancellationRequested)
            {
                Logger.LogInformation($"Calling Direct Method from cloud on device {Settings.Current.DeviceId} targeting module [{Settings.Current.TargetModuleId}] with count {directMethodCount}.");

                try
                {
                    CloudToDeviceMethodResult result = await serviceClient.InvokeDeviceMethodAsync(deviceId, targetModuleId, cloudToDeviceMethod, CancellationToken.None);

                    string statusMessage = $"Calling Direct Method from cloud with count {directMethodCount} returned with status code {result.Status}";
                    if (result.Status == (int)HttpStatusCode.OK)
                    {
                        Logger.LogDebug(statusMessage);
                    }
                    else
                    {
                        Logger.LogError(statusMessage);
                    }

                    await ReportStatus(targetModuleId, result, analyzerClient);
                    directMethodCount++;
                }
                catch (Exception e)
                {
                    Logger.LogError($"Exception caught with count {directMethodCount}: {e}");
                }

                await Task.Delay(delay, cts.Token);
            }

            Logger.LogInformation("CallDirectMethodFromCloud finished.");
            await serviceClient.CloseAsync();
        }

        static async Task ReportStatus(string moduleId, CloudToDeviceMethodResult result, AnalyzerClient analyzerClient)
        {
            try
            {
                await analyzerClient.ReportResultAsync(new TestOperationResult { Source = moduleId, Result = result.Status.ToString(), CreatedAt = DateTime.UtcNow, Type = Enum.GetName(typeof(TestOperationResultType), TestOperationResultType.LegacyDirectMethod) });
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed call to report status to analyzer");
            }
        }
    }
}
