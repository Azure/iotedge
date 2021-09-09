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

                var transportType = configuration.GetValue("ClientTransportType", Microsoft.Azure.Devices.Client.TransportType.Mqtt);

                Logger.LogInformation("Make Client");
                using (ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    transportType,
                    new ClientOptions(),
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger))
                using (MetricsScraper scraper = new MetricsScraper(new List<string> { "http://edgeHub:9600/metrics", "http://edgeAgent:9600/metrics" }))
                {
                    Logger.LogInformation("Open Async");
                    await moduleClient.OpenAsync();

                    Logger.LogInformation("Set method handler");
                    await moduleClient.SetMethodHandlerAsync(
                        "ValidateMetrics",
                        async (MethodRequest methodRequest, object _) =>
                        {
                            Logger.LogInformation("Validating metrics");

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

                            return new MethodResponse(Encoding.UTF8.GetBytes(testReporter.ReportResults()), (int)HttpStatusCode.OK);
                        },
                        null);

                    Logger.LogInformation("Ready to validate metrics");
                    await cts.Token.WhenCanceled();
                }

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
