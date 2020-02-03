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

                using (ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync())
                using (MetricsScraper scraper = new MetricsScraper(new List<string> { "http://edgeHub:9600/metrics", "http://edgeAgent:9600/metrics" }))
                {
                    await moduleClient.OpenAsync();
                    await moduleClient.SetMethodHandlerAsync(
                        "ValidateMetrics",
                        async (MethodRequest methodRequest, object _) =>
                        {
                            Console.WriteLine("Validating metrics");

                            TestReporter testReporter = new TestReporter("Metrics Validation");
                            List<TestBase> tests = new List<TestBase>
                            {
                                new ValidateNumberOfMessagesSent(testReporter, scraper, moduleClient),
                                new ValidateDocumentedMetrics(testReporter, scraper, moduleClient),
                                new ValidateHostRanges(testReporter, scraper, moduleClient),
                            };

                            await Task.WhenAll(tests.Select(test => test.Start(cts.Token)));
                            return new MethodResponse(Encoding.UTF8.GetBytes(testReporter.ReportResults()), (int)HttpStatusCode.OK);
                        },
                        null);

                    Console.WriteLine("Ready to validate metrics");
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
