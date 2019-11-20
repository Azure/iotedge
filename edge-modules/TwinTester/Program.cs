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
                await moduleClient.OpenAsync();

                AnalyzerClient analyzerClient = new AnalyzerClient { BaseUrl = Settings.Current.AnalyzerUrl };

                Storage storage = new Storage();
                storage.Init(Settings.Current.StoragePath, new SystemEnvironment(), Settings.Current.StorageOptimizeForPerformance);

                TwinOperator twinOperator = new TwinOperator(registryManager, moduleClient, analyzerClient, storage);
                await twinOperator.InitializeModuleTwin();

                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);
                Task updateLoop = PerformRecurringUpdates(twinOperator, Settings.Current.TwinUpdateFrequency, cts);
                Task validationLoop = PerformRecurringValidation(twinOperator, Settings.Current.TwinUpdateFailureThreshold, cts);
                await Task.WhenAll(updateLoop, validationLoop);

                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error occurred during twin test setup.\r\n{ex}");
            }
        }

        static async Task PerformRecurringUpdates(TwinOperator twinOperator, TimeSpan twinUpdateFrequency, CancellationTokenSource cts)
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await twinOperator.PerformUpdates();
                await Task.Delay(twinUpdateFrequency);
            }

            Logger.LogInformation("PerformRecurringUpdates finished.");
        }

        static async Task PerformRecurringValidation(TwinOperator twinOperator, TimeSpan twinUpdateFailureThreshold, CancellationTokenSource cts)
        {
            TimeSpan validationInterval = new TimeSpan(Settings.Current.TwinUpdateFailureThreshold.Ticks / 4);
            while (!cts.Token.IsCancellationRequested)
            {
                await twinOperator.PerformValidation();
                await Task.Delay(validationInterval);
            }

            Logger.LogInformation("PerformRecurringValidation finished.");
        }
    }
}
