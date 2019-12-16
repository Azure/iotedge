// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class TwinAllOperationsResultHandler : ITwinTestResultHandler
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinAllOperationsResultHandler));
        readonly AnalyzerClient analyzerClient;
        readonly string moduleId;
        readonly TwinEventStorage storage;

        public TwinAllOperationsResultHandler(Uri analyzerClientUri, TwinEventStorage storage, string moduleId)
        {
            this.analyzerClient = new AnalyzerClient() { BaseUrl = analyzerClientUri.AbsoluteUri };
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

        public async Task HandleDesiredPropertyUpdateAsync(string status)
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

        public async Task HandleReportedPropertyUpdateAsync(string status)
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
            return this.CallAnalyzer(status);
        }

        public Task HandleReportedPropertyUpdateExceptionAsync(string propertyKey, string failureStatus)
        {
            return this.CallAnalyzer(failureStatus);
        }

        async Task CallAnalyzer(string failureStatus)
        {
            try
            {
                await this.analyzerClient.ReportResultAsync(new TestOperationResult { Source = this.moduleId, Result = failureStatus, CreatedAt = DateTime.UtcNow, Type = "LegacyTwin" });
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed call to report status to analyzer.");
            }
        }
    }
}
