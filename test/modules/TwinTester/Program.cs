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
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinTester));

        static async Task Main()
        {
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            Logger.LogInformation($"Starting twin tester with the following settings:\r\n{Settings.Current}");

            ITwinTestInitializer twinOperator = null;
            try
            {
                using (RegistryManager registryManager = RegistryManager.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString))
                {
                    twinOperator = await GetTwinOperatorAsync(registryManager);
                    await twinOperator.StartAsync(cts.Token);
                    await Task.Delay(Settings.Current.TestDuration, cts.Token);

                    Logger.LogInformation($"Test run completed after {Settings.Current.TestDuration}");
                    twinOperator.Stop();

                    await cts.Token.WhenCanceled();
                    completed.Set();
                    handler.ForEach(h => GC.KeepAlive(h));
                    Logger.LogInformation("TwinTester exiting.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error occurred during twin test setup.");
                twinOperator?.Stop();
            }
        }

        static async Task<ITwinTestInitializer> GetTwinOperatorAsync(RegistryManager registryManager)
        {
            switch (Settings.Current.TwinTestMode)
            {
                case TwinTestMode.TwinCloudOperations:
                    return await TwinCloudOperationsInitializer.CreateAsync(registryManager, new TwinEdgeOperationsResultHandler(Settings.Current.ReporterUrl, Settings.Current.ModuleId, Settings.Current.TrackingId));
                case TwinTestMode.TwinEdgeOperations:
                    ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                        Settings.Current.TransportType,
                        new ClientOptions(),
                        ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                        ModuleUtil.DefaultTransientRetryStrategy,
                        Logger);
                    return await TwinEdgeOperationsInitializer.CreateAsync(registryManager, moduleClient, new TwinEdgeOperationsResultHandler(Settings.Current.ReporterUrl, Settings.Current.ModuleId, Settings.Current.TrackingId));
                default:
                    return await GetTwinAllOperationsInitializer(registryManager, Settings.Current.ReporterUrl);
            }
        }

        static async Task<ITwinTestInitializer> GetTwinAllOperationsInitializer(RegistryManager registryManager, Uri reportUrl)
        {
            ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                Settings.Current.TransportType,
                new ClientOptions(),
                ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                ModuleUtil.DefaultTransientRetryStrategy,
                Logger);

            TwinEventStorage storage = new TwinEventStorage();
            storage.Init(Settings.Current.StoragePath, new SystemEnvironment(), Settings.Current.StorageOptimizeForPerformance);
            var resultHandler = new TwinAllOperationsResultHandler(reportUrl, storage, Settings.Current.ModuleId);
            return await TwinAllOperationsInitializer.CreateAsync(registryManager, moduleClient, resultHandler, storage);
        }
    }
}
