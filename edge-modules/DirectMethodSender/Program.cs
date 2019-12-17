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

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);
            try
            {
                IDirectMethodClient client;
                switch (Settings.Current.RoutingAgency)
                {
                    case RoutingAgency.EdgeHub:
                        client = await ModuleClientWrapper.CreateAsync(
                                Settings.Current.TransportType,
                                ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                                ModuleUtil.DefaultTransientRetryStrategy,
                                Logger);
                        break;

                    case RoutingAgency.Upstream:
                        client = ServiceClientWrapper.Create(
                                Settings.Current.ServiceClientConnectionString.Expect(() => new ArgumentException("ServiceClientConnectionString is null")),
                                (Microsoft.Azure.Devices.TransportType)Settings.Current.TransportType,
                                Logger);
                        break;

                    default:
                        throw new NotImplementedException("Invalid RoutingAgency type");
                }

                await client.OpenClientAsync();
                while (!cts.Token.IsCancellationRequested)
                {
                    HttpStatusCode result = await client.InvokeDirectMethodAsync(cts);

                    Option<Uri> analyzerUrl = Settings.Current.AnalyzerUrl;
                    await analyzerUrl.ForEachAsync(
                        async (Uri uri) =>
                        {
                            AnalyzerClient analyzerClient = new AnalyzerClient { BaseUrl = uri.AbsoluteUri };
                            await ReportStatus(Settings.Current.TargetModuleId, result, analyzerClient);
                        },
                        async () =>
                        {
                            await client.SendEventAsync("AnyOutput", "Direct Method call succeeded.");
                        });

                    await Task.Delay(Settings.Current.DirectMethodDelay, cts.Token);
                }

                await client.CloseClientAsync();
                await cts.Token.WhenCanceled();

                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error occurred during direct method sender test setup");
            }

            Logger.LogInformation("DirectMethodSender Main() finished.");
            return 0;
        }

        static async Task ReportStatus(string moduleId, HttpStatusCode result, AnalyzerClient analyzerClient)
        {
            try
            {
                await analyzerClient.ReportResultAsync(new TestOperationResult { Source = moduleId, Result = result.ToString(), CreatedAt = DateTime.UtcNow, Type = Enum.GetName(typeof(TestOperationResultType), TestOperationResultType.LegacyDirectMethod) });
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
            }
        }
    }
}
