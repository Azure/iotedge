// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResultCoordinatorClient;

    class TwinEdgeOperationsResultHandler : ITwinTestResultHandler
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinEdgeOperationsResultHandler));
        readonly TestResultCoordinatorClient trcClient;
        readonly string moduleId;

        public TwinEdgeOperationsResultHandler(Uri analyzerClientUri, string moduleId)
        {
            this.trcClient = new TestResultCoordinatorClient() { BaseUrl = analyzerClientUri.AbsoluteUri };
            this.moduleId = moduleId;
        }

        public Task HandleDesiredPropertyReceivedAsync(TwinCollection desiredProperties)
        {
            return this.SendReportAsync(StatusCode.DesiredPropertyReceived, desiredProperties.ToString());
        }

        public Task HandleDesiredPropertyUpdateAsync(string details)
        {
            return this.SendReportAsync(StatusCode.DesiredPropertyUpdated, details);
        }

        public Task HandleReportedPropertyUpdateAsync(string status)
        {
            return this.SendReportAsync(StatusCode.ReportedPropertyReceived, status);
        }

        public Task HandleTwinValidationStatusAsync(string status)
        {
            return Task.CompletedTask;
        }

        public Task HandleReportedPropertyUpdateExceptionAsync(string propertyKey, string failureStatus)
        {
            return this.SendReportAsync(StatusCode.ReportedPropertyUpdateCallFailure, propertyKey, failureStatus);
        }

        async Task SendReportAsync(StatusCode statusCode, string details, string exception = "")
        {
            var result = new TwinTestResult() { Operation = statusCode.ToString(), TwinProperty = details, ErrorMessage = exception };
            await ModuleUtil.ReportStatus(trcClient, Logger, this.moduleId, result.ToString(), TestOperationResultType.Twin.ToString());
        }

        class TwinTestResult
        {
            public string Operation { get; set; }

            public string TwinProperty { get; set; }

            public string ErrorMessage { get; set; }

            public override string ToString()
            {
                return JsonConvert.SerializeObject(this, Formatting.Indented);
            }
        }
    }
}
