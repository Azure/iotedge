// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("TwinTester");

        static async Task Main()
        {
            Logger.LogInformation($"Starting twin tester with the following settings:\r\n{Settings.Current}");

            try
            {
                RegistryManager registryManager = RegistryManager.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString);

                ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);

                AnalyzerClient analyzerClient = new AnalyzerClient { BaseUrl = Settings.Current.AnalyzerUrl };

                Storage storage = new Storage();
                storage.Init(Settings.Current.StoragePath, new SystemEnvironment(), Settings.Current.StorageOptimizeForPerformance);

                TwinOperator twinOperator = new TwinOperator(registryManager, moduleClient, analyzerClient, storage);

                using (var timers = new Timers())
                {
                    // setup the twin update timer
                    timers.Add(
                        Settings.Current.TwinUpdateFrequency,
                        Settings.Current.JitterFactor,
                        () => twinOperator.PerformUpdates());

                    TimeSpan validationInterval = new TimeSpan(Settings.Current.TwinUpdateFailureThreshold.Ticks / 4);
                    timers.Add(
                        validationInterval,
                        0,
                        () => twinOperator.PerformValidation());
                    timers.Start();
                    Logger.LogInformation("TwinTester starting twin tests.");

                    (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                    await cts.Token.WhenCanceled();
                    Logger.LogInformation("Stopping timers.");
                    timers.Stop();
                    Logger.LogInformation("Closing connection to Edge Hub.");
                    await moduleClient.CloseAsync();

                    completed.Set();
                    handler.ForEach(h => GC.KeepAlive(h));
                    Logger.LogInformation("Twin tests complete. Exiting.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error occurred during twin test setup.\r\n{ex}");
            }
        }
    }
}
