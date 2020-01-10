// Copyright (c) Microsoft. All rights reserved.
namespace MetricsValidator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
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
                Logger.LogInformation("DirectMethodReceiver Main() started.");

                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config/appsettings.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                TestReporter testReporter = new TestReporter("Metrics Validation");

                using (ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync())
                using (MetricsScraper scraper = new MetricsScraper(new List<string> { "http://edgeHub:9600/metrics", "http://edgeAgent:9600/metrics" }))
                {
                    await moduleClient.OpenAsync();
                    await Task.Delay(5000);

                    await new ValidateNumberOfMessagesSent(testReporter, scraper, moduleClient).Start(cts.Token);
                    // await new ValidateDocumentedMetrics(testReporter, scraper).Start(cts.Token);
                    await testReporter.ReportResults(moduleClient, cts.Token);
                }

                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
                Logger.LogInformation("DirectMethodReceiver Main() finished.");
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
            }

            return 0;
        }
    }
}
