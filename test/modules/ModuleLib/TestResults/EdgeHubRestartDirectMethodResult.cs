// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using System.Net;
    using Newtonsoft.Json;

    public class EdgeHubRestartDirectMethodResult : DirectMethodTestResult
    {
        public EdgeHubRestartDirectMethodResult(
            string source,
            DateTime createdAt,
            string trackingId,
            Guid batchId,
            string sequenceNumber,
            DateTime edgeHubRestartedTime,
            DateTime directMethodCompletedTime,
            HttpStatusCode directMethodCompletedStatusCode)
            : base(
                source,
                createdAt,
                trackingId,
                batchId,
                sequenceNumber,
                directMethodCompletedStatusCode,
                TestOperationResultType.EdgeHubRestartDirectMethod)
        {
            this.EdgeHubRestartedTime = edgeHubRestartedTime;
            this.DirectMethodCompletedTime = directMethodCompletedTime;
            this.DirectMethodCompletedStatusCode = directMethodCompletedStatusCode;
        }

        public DateTime EdgeHubRestartedTime { get; set; }

        public DateTime DirectMethodCompletedTime { get; set; }

        public HttpStatusCode DirectMethodCompletedStatusCode { get; set; }

        public override string GetFormattedResult()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
