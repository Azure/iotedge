// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("DirectMethodSender");

        public static int Main() => MainAsync().Result;

        public static async Task<int> MainAsync()
        {
            Logger.LogInformation($"Starting DirectMethodSender with the following settings:\r\n{Settings.Current}");

            ModuleClient moduleClient = null;
            try
            {
                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);

                string analyzerUrl = Settings.Current.AnalyzerUrl;
                if (!string.IsNullOrWhiteSpace(analyzerUrl))
                {
                    Uri analyzerUri = new Uri(analyzerUrl);
                    AnalyzerClient analyzerClient = new AnalyzerClient { BaseUrl = analyzerUri.AbsoluteUri };
                    Action<MethodResponse> reportResult = async (response) => await ReportStatus(Settings.Current.TargetModuleId, response, analyzerClient);
                    await StartDirectMethdTests(moduleClient, reportResult, Settings.Current.DirectMethodDelay, cts);
                }
                else
                {
                    Action<MethodResponse> reportResult = async (response) => await moduleClient.SendEventAsync("AnyOutput", new Message(Encoding.UTF8.GetBytes("Direct Method Call succeeded.")));
                    await StartDirectMethdTests(moduleClient, reportResult, Settings.Current.DirectMethodDelay, cts);
                }

                await moduleClient.CloseAsync();
                await cts.Token.WhenCanceled();

                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
                Logger.LogInformation("DirectMethodSender Main() finished.");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error occurred during direct method sender test setup");
            }
            finally
            {
                moduleClient?.Dispose();
            }

            return 0;
        }

        static async Task StartDirectMethdTests(
            ModuleClient moduleClient,
            Action<MethodResponse> reportResult,
            TimeSpan delay,
            CancellationTokenSource cts)
        {
            var request = new MethodRequest("HelloWorldMethod", Encoding.UTF8.GetBytes("{ \"Message\": \"Hello\" }"));
            string deviceId = Settings.Current.DeviceId;
            string targetModuleId = Settings.Current.TargetModuleId;
            int directMethodCount = 1;

            while (!cts.Token.IsCancellationRequested)
            {
                Logger.LogInformation($"Calling Direct Method on device {deviceId} targeting module {targetModuleId}.");

                try
                {
                    MethodResponse response = await moduleClient.InvokeMethodAsync(deviceId, targetModuleId, request);

                    string statusMessage = $"Calling Direct Method with count {directMethodCount} returned with status code {response.Status}";
                    if (response.Status == (int)HttpStatusCode.OK)
                    {
                        Logger.LogDebug(statusMessage);
                    }
                    else
                    {
                        Logger.LogError(statusMessage);
                    }

                    reportResult.Invoke(response);
                    directMethodCount++;
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Exception caught");
                }

                await Task.Delay(delay, cts.Token);
            }
        }

        static async Task ReportStatus(string moduleId, MethodResponse response, AnalyzerClient analyzerClient)
        {
            try
            {
                await analyzerClient.ReportResultAsync(new TestOperationResult { Source = moduleId, Result = response.Status.ToString(), CreatedAt = DateTime.UtcNow, Type = Enum.GetName(typeof(TestOperationResultType), TestOperationResultType.LegacyDirectMethod) });
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
            }
        }
    }
}
