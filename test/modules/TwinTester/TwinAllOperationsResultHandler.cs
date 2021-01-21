// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class TwinAllOperationsResultHandler : ITwinTestResultHandler
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinAllOperationsResultHandler));
        readonly TestResultReportingClient testResultReportingClient;
        readonly string moduleId;
        readonly TwinEventStorage storage;

        public TwinAllOperationsResultHandler(Uri reportUrl, TwinEventStorage storage, string moduleId)
        {
            this.testResultReportingClient = new TestResultReportingClient { BaseUrl = reportUrl.AbsoluteUri };
            this.moduleId = moduleId;
            this.storage = storage;
        }

        public async Task HandleDesiredPropertyReceivedAsync(TwinCollection desiredProperties)
        {
            try
            {
                foreach (dynamic twinUpdate in desiredProperties)
                {
                    KeyValuePair<string, object> pair = (KeyValuePair<string, object>)twinUpdate;
                    await this.storage.AddDesiredPropertyReceivedAsync(pair.Key);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed call to report status to storage.");
            }
        }

        public async Task HandleDesiredPropertyUpdateAsync(string status, string value)
        {
            try
            {
                await this.storage.AddDesiredPropertyUpdateAsync(status);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed call to report status to storage");
            }
        }

        public async Task HandleReportedPropertyUpdateAsync(string status, string value)
        {
            try
            {
                await this.storage.AddReportedPropertyUpdateAsync(status);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed call to report status to storage.");
            }
        }

        public Task HandleTwinValidationStatusAsync(string status)
        {
            return this.SendStatus(status);
        }

        public Task HandleReportedPropertyUpdateExceptionAsync(string failureStatus)
        {
            return this.SendStatus(failureStatus);
        }

        async Task SendStatus(string status)
        {
            var result = new LegacyTwinTestResult(this.moduleId, DateTime.UtcNow, status);
            Logger.LogDebug($"Sending report {result.GetFormattedResult()}");
            await ModuleUtil.ReportTestResultAsync(this.testResultReportingClient, Logger, result);
        }
    }
}
