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
    using Newtonsoft.Json;

    class TwinEdgeOperationsInitializer : ITwinTestInitializer
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinCloudOperationsInitializer));
        readonly ReportedPropertyUpdater reportedPropertyUpdater;
        readonly DesiredPropertyReceiver desiredPropertiesReceiver;
        readonly IotHubServiceClient serviceClient;
        PeriodicTask periodicUpdate;

        TwinEdgeOperationsInitializer(IotHubServiceClient serviceClient, IotHubModuleClient moduleClient, ITwinTestResultHandler reporter, int reportedPropertyUpdateCounter)
        {
            this.serviceClient = serviceClient;
            this.reportedPropertyUpdater = new ReportedPropertyUpdater(moduleClient, reporter, reportedPropertyUpdateCounter);
            this.desiredPropertiesReceiver = new DesiredPropertyReceiver(moduleClient, reporter);
        }

        public static Task<TwinEdgeOperationsInitializer> CreateAsync(IotHubServiceClient serviceClient, IotHubModuleClient moduleClient, ITwinTestResultHandler reporter)
        {
            return Task.FromResult(new TwinEdgeOperationsInitializer(serviceClient, moduleClient, reporter, 0));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await this.desiredPropertiesReceiver.UpdateAsync();
            Logger.LogInformation($"Waiting for {Settings.Current.TestStartDelay} based on TestStartDelay setting before starting.");
            await Task.Delay(Settings.Current.TestStartDelay, cancellationToken);
            await this.LogEdgeDeviceTwin();
            this.periodicUpdate = new PeriodicTask(this.UpdateAsync, Settings.Current.TwinUpdateFrequency, Settings.Current.TwinUpdateFrequency, Logger, "TwinReportedPropertiesUpdate");
        }

        async Task LogEdgeDeviceTwin()
        {
            try
            {
                ClientTwin twin = await this.serviceClient.Twins.GetAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId);
                Logger.LogInformation($"Start state of module twin: {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Failure to get twin");
            }
        }

        public void Stop()
        {
            this.periodicUpdate?.Dispose();
        }

        async Task UpdateAsync(CancellationToken cancellationToken)
        {
            await this.reportedPropertyUpdater.UpdateAsync();
        }
    }
}
