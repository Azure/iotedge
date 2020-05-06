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
        readonly ModuleClient moduleClient;
        readonly ITwinTestResultHandler reporter;
        int reportedPropertyUpdateCounter;

        public ReportedPropertyUpdater(ModuleClient moduleClient, ITwinTestResultHandler reporter, int reportedPropertyUpdateCounter)
        {
            this.moduleClient = moduleClient;
            this.reporter = reporter;
            this.reportedPropertyUpdateCounter = reportedPropertyUpdateCounter;
        }

        public async Task UpdateAsync()
        {
            try
            {
                string reportedPropertyUpdateValue = new string('1', Settings.Current.TwinUpdateSize); // dummy twin update can to be any number
                var twin = new TwinCollection();
                twin[this.reportedPropertyUpdateCounter.ToString()] = reportedPropertyUpdateValue;

                await this.moduleClient.UpdateReportedPropertiesAsync(twin);
                Logger.LogInformation($"Reported property updated {this.reportedPropertyUpdateCounter}");

                await this.ReportAsync(reportedPropertyUpdateValue);
            }
            catch (Exception e)
            {
                string failureStatus = $"{(int)StatusCode.ReportedPropertyUpdateCallFailure}: Failed call to update reported properties";
                Logger.LogError(e, failureStatus);
                await this.reporter.HandleReportedPropertyUpdateExceptionAsync(failureStatus);
            }
        }

        async Task ReportAsync(string value)
        {
            try
            {
                await this.reporter.HandleReportedPropertyUpdateAsync(this.reportedPropertyUpdateCounter.ToString(), value);
                this.reportedPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed adding reported property update to storage.");
            }
        }
    }
}
