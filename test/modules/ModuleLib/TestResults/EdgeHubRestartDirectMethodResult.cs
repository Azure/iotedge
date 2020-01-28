// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util;
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
            HttpStatusCode edgeHubRestartStatusCode,
            DateTime directMethodCompletedTime,
            HttpStatusCode directMethodCompletedStatusCode)
            : base(
                source,
                createdAt,
                trackingId,
                batchId,
                sequenceNumber,
                directMethodCompletedStatusCode)
        {
            this.EdgeHubRestartedTime = Preconditions.CheckNotNull(edgeHubRestartedTime, nameof(edgeHubRestartedTime));
            this.EdgeHubRestartStatusCode = Preconditions.CheckNotNull(edgeHubRestartStatusCode, nameof(edgeHubRestartStatusCode));
            this.DirectMethodCompletedTime = Preconditions.CheckNotNull(directMethodCompletedTime, nameof(directMethodCompletedTime));
            this.DirectMethodCompletedStatusCode = Preconditions.CheckNotNull(directMethodCompletedStatusCode, nameof(directMethodCompletedStatusCode));
        }

        DateTime EdgeHubRestartedTime { get; set; }

        public HttpStatusCode EdgeHubRestartStatusCode { get; set; }

        public DateTime DirectMethodCompletedTime { get; set; }

        public HttpStatusCode DirectMethodCompletedStatusCode { get; set; }

        public string GetDirectMethodTestResult()
        {
            return base.GetFormattedResult();
        }

        public override string GetFormattedResult()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
