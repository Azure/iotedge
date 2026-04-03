// Copyright (c) Microsoft. All rights reserved.
namespace MetricsValidator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MetricsValidator.Tests;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("MetricsValidator");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            try
            {
                Logger.LogInformation("Validate Metrics Main() started.");

                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config/appsettings.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                var transportType = configuration.GetValue("ClientTransportType", Microsoft.Azure.Devices.Edge.ModuleUtil.TransportType.Mqtt);

                Logger.LogInformation("Make Client");
                IotHubModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    transportType,
                    null,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);
                using (MetricsScraper scraper = new MetricsScraper(new List<string> { "http://edgeHub:9600/metrics", "http://edgeAgent:9600/metrics" }))
                {
                    Logger.LogInformation("Open Async");
                    await moduleClient.OpenAsync();

                    SemaphoreSlim directMethodProcessingLock = new SemaphoreSlim(1, 1);

                    Logger.LogInformation("Set method handler");
                    await moduleClient.SetDirectMethodCallbackAsync(
                        async (DirectMethodRequest methodRequest) =>
                        {
                            if (methodRequest.MethodName == "ValidateMetrics")
                            {
                                Logger.LogInformation("Received method call to validate metrics");
                                try
                                {
                                    await directMethodProcessingLock.WaitAsync();

                                    // Delay to give buffer between potentially repeated direct method calls
                                    await Task.Delay(TimeSpan.FromSeconds(5));

                                    Logger.LogInformation("Starting metrics validation");

                                    TestReporter testReporter = new TestReporter("Metrics Validation");
                                    List<TestBase> tests = new List<TestBase>
                                    {
                                        new ValidateMessages(testReporter, scraper, moduleClient, transportType),
                                        new ValidateDocumentedMetrics(testReporter, scraper, moduleClient),
                                        // new ValidateHostRanges(testReporter, scraper, moduleClient),
                                    };

                                    using (testReporter.MeasureDuration())
                                    {
                                        await Task.WhenAll(tests.Select(test => test.Start(cts.Token)));
                                    }

                                    var result = new DirectMethodResponse((int)HttpStatusCode.OK)
                                    {
                                        Payload = Encoding.UTF8.GetBytes(testReporter.ReportResults())
                                    };

                                    Logger.LogInformation($"Finished validating metrics. Result size: {result.Payload.Length}");

                                    return result;
                                }
                                finally
                                {
                                    directMethodProcessingLock.Release();
                                }
                            }

                            return new DirectMethodResponse((int)HttpStatusCode.NotFound);
                        });

                    moduleClient.ConnectionStatusChangeCallback = (ConnectionStatusInfo connectionStatusInfo)
                        => Logger.LogWarning($"Module to Edge Hub connection changed Status: {connectionStatusInfo.Status} Reason: {connectionStatusInfo.ChangeReason}");

                    Logger.LogInformation("Ready to validate metrics");
                    await cts.Token.WhenCanceled();
                }

                await moduleClient.DisposeAsync();

                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
                Logger.LogInformation("Validate Metrics Main() finished.");
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
            }

            return 0;
        }
    }
}
