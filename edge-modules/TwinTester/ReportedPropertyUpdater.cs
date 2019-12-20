// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class ReportedPropertyUpdater : ITwinOperation
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(ReportedPropertyUpdater));
        readonly RegistryManager registryManager;
        readonly ModuleClient moduleClient;
        readonly ITwinTestResultHandler reporter;
        readonly TwinState twinState;

        public ReportedPropertyUpdater(RegistryManager registryManager, ModuleClient moduleClient, ITwinTestResultHandler reporter, TwinState twinState)
        {
            this.registryManager = registryManager;
            this.moduleClient = moduleClient;
            this.reporter = reporter;
            this.twinState = twinState;
        }

        public async Task UpdateAsync()
        {
            try
            {
                string reportedPropertyUpdateValue = new string('1', Settings.Current.TwinUpdateSize); // dummy twin update can to be any number
                var twin = new TwinCollection();
                twin[this.twinState.ReportedPropertyUpdateCounter.ToString()] = reportedPropertyUpdateValue;

                await this.moduleClient.UpdateReportedPropertiesAsync(twin);
                Logger.LogInformation($"Reported property updated {this.twinState.ReportedPropertyUpdateCounter}");

                await this.ReportAsync();
            }
            catch (Exception e)
            {
                string failureStatus = $"{(int)StatusCode.ReportedPropertyUpdateCallFailure}: Failed call to update reported properties";
                Logger.LogError(e, failureStatus);
                await this.reporter.HandleReportedPropertyUpdateExceptionAsync(this.twinState.ReportedPropertyUpdateCounter.ToString(), failureStatus);
            }
        }

        async Task ReportAsync()
        {
            try
            {
                await this.reporter.HandleReportedPropertyUpdateAsync(this.twinState.ReportedPropertyUpdateCounter.ToString());
                this.twinState.ReportedPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed adding reported property update to storage.");
            }
        }
    }
}
