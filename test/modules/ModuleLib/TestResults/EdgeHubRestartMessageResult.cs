// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class EdgeHubRestartMessageResult : MessageTestResult
    {
        public EdgeHubRestartMessageResult(
            string source,
            DateTime createdAt,
            string trackingId,
            string batchId,
            string sequenceNumber,
            DateTime edgeHubRestartedTime,
            DateTime messageCompletedTime,
            HttpStatusCode messageCompletedStatusCode)
            : base(source, createdAt, TestOperationResultType.EdgeHubRestartMessage)
        {
            this.TrackingId = trackingId;
            this.BatchId = batchId;
            this.SequenceNumber = sequenceNumber;
            this.EdgeHubRestartedTime = edgeHubRestartedTime;
            this.MessageCompletedTime = messageCompletedTime;
            this.MessageCompletedStatusCode = messageCompletedStatusCode;
        }

        DateTime EdgeHubRestartedTime { get; set; }

        public DateTime MessageCompletedTime { get; set; }

        public HttpStatusCode MessageCompletedStatusCode { get; set; }

        public string GetMessageTestResult()
        {
            return base.GetFormattedResult();
        }

        public override string GetFormattedResult()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
