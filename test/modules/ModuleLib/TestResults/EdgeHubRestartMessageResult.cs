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
            HttpStatusCode edgeHubRestartStatusCode,
            DateTime messageCompletedTime,
            HttpStatusCode messageCompletedStatusCode,
            uint restartSequenceNumber)
            : base(source, createdAt)
        {
            this.TrackingId = trackingId;
            this.BatchId = batchId;
            this.SequenceNumber = sequenceNumber;
            this.EdgeHubRestartedTime = Preconditions.CheckNotNull(edgeHubRestartedTime, nameof(edgeHubRestartedTime));
            this.EdgeHubRestartStatusCode = Preconditions.CheckNotNull(edgeHubRestartStatusCode, nameof(edgeHubRestartStatusCode));
            this.MessageCompletedTime = Preconditions.CheckNotNull(messageCompletedTime, nameof(messageCompletedTime));
            this.MessageCompletedStatusCode = Preconditions.CheckNotNull(messageCompletedStatusCode, nameof(messageCompletedStatusCode));
            this.RestartSequenceNumber = Preconditions.CheckNotNull(restartSequenceNumber, nameof(restartSequenceNumber));
        }

        DateTime EdgeHubRestartedTime { get; set; }

        public HttpStatusCode EdgeHubRestartStatusCode { get; set; }

        public DateTime MessageCompletedTime { get; set; }

        public HttpStatusCode MessageCompletedStatusCode { get; set; }

        public uint RestartSequenceNumber { get; set; }

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
