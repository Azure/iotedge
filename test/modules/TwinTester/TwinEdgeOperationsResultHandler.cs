// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class TwinEdgeOperationsResultHandler : ITwinTestResultHandler
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinEdgeOperationsResultHandler));
        readonly TestResultReportingClient testResultReportingClient;
        readonly string moduleId;
        readonly string trackingId;

        public TwinEdgeOperationsResultHandler(Uri reportUrl, string moduleId, Option<string> trackingId)
        {
            this.testResultReportingClient = new TestResultReportingClient { BaseUrl = reportUrl.AbsoluteUri };
            this.moduleId = moduleId;
            this.trackingId = trackingId.Expect(() => new ArgumentNullException(nameof(trackingId)));
        }

        public Task HandleDesiredPropertyReceivedAsync(TwinCollection desiredProperties)
        {
            return this.SendReportAsync($"{this.moduleId}.desiredReceived", StatusCode.DesiredPropertyReceived, desiredProperties);
        }

        public Task HandleDesiredPropertyUpdateAsync(string propertyKey, string value)
        {
            TwinCollection properties = this.CreateTwinCollection(propertyKey, value);
            return this.SendReportAsync($"{this.moduleId}.desiredUpdated", StatusCode.DesiredPropertyUpdated, properties);
        }

        public Task HandleReportedPropertyUpdateAsync(string propertyKey, string value)
        {
            TwinCollection properties = this.CreateTwinCollection(propertyKey, value);
            return this.SendReportAsync($"{this.moduleId}.reportedUpdated", StatusCode.ReportedPropertyUpdated, properties);
        }

        public Task HandleTwinValidationStatusAsync(string status)
        {
            return Task.CompletedTask;
        }

        public Task HandleReportedPropertyUpdateExceptionAsync(string failureStatus)
        {
            return Task.CompletedTask;
        }

        TwinCollection CreateTwinCollection(string propertyKey, string value)
        {
            var properties = new TwinCollection();
            properties[propertyKey] = value;

            return properties;
        }

        async Task SendReportAsync(string source, StatusCode statusCode, TwinCollection details, string exception = "")
        {
            var result = new TwinTestResult(source, DateTime.UtcNow)
            {
                Operation = statusCode.ToString(),
                Properties = details,
                ErrorMessage = exception,
                TrackingId = this.trackingId
            };
            Logger.LogDebug($"Sending report {result.GetFormattedResult()}");
            await ModuleUtil.ReportTestResultAsync(this.testResultReportingClient, Logger, result);
        }
    }
}
