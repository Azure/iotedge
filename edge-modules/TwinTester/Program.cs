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

            try
            {
                RegistryManager registryManager = RegistryManager.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString);

                using (ITwinTestInitializer twinOperator = await GetTwinOperatorAsync(registryManager, Settings.Current.AnalyzerUrl))
                {
                    await twinOperator.Start();

                    await cts.Token.WhenCanceled();
                    completed.Set();
                    handler.ForEach(h => GC.KeepAlive(h));
                    Logger.LogInformation("TwinTester exiting.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error occurred during twin test setup.");
            }
        }

        static async Task<ITwinTestInitializer> GetTwinOperatorAsync(RegistryManager registryManager, Uri analyzerClientUri)
        {
            switch (Settings.Current.TestMode)
            {
                case TestMode.TwinCloudOperations:
                    return await TwinCloudOperationsInitializer.CreateAsync(registryManager, new TwinEdgeOperationsResultHandler(analyzerClientUri, Settings.Current.ModuleId));
                case TestMode.TwinEdgeOperations:
                    ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                        Settings.Current.TransportType,
                        ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                        ModuleUtil.DefaultTransientRetryStrategy,
                        Logger);
                    return await TwinEdgeOperationsInitializer.CreateAsync(registryManager, moduleClient, new TwinEdgeOperationsResultHandler(analyzerClientUri, Settings.Current.ModuleId));
                default:
                    return await GetTwinAllOperationsInitializer(registryManager, analyzerClientUri);
            }
        }

        static async Task<ITwinTestInitializer> GetTwinAllOperationsInitializer(RegistryManager registryManager, Uri analyzerClientUri)
        {
            ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                Settings.Current.TransportType,
                ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                ModuleUtil.DefaultTransientRetryStrategy,
                Logger);

            TwinEventStorage storage = new TwinEventStorage();
            storage.Init(Settings.Current.StoragePath, new SystemEnvironment(), Settings.Current.StorageOptimizeForPerformance);
            var resultHandler = new TwinAllOperationsResultHandler(analyzerClientUri, storage, Settings.Current.ModuleId);
            return await TwinAllOperationsInitializer.CreateAsync(registryManager, moduleClient, resultHandler, storage);
        }
    }
}
