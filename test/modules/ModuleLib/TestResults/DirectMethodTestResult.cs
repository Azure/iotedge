// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DirectMethodTestResult : TestResultBase
    {
        public DirectMethodTestResult(
            string source,
            DateTime createdAt,
            string trackingId,
            Guid batchId,
            long sequenceNumber,
            string result)
            : base(source, TestOperationResultType.DirectMethod, createdAt)
        {
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.BatchId = Preconditions.CheckNotNull(batchId, nameof(batchId)).ToString();
            this.SequenceNumber = Preconditions.CheckNotNull(sequenceNumber, nameof(sequenceNumber)).ToString();
            this.Result = Preconditions.CheckNonWhiteSpace(result, nameof(result));
        }

        public string TrackingId { get; set; }

        public string BatchId { get; set; }

        public string SequenceNumber { get; set; }

        public string Result { get; set; }

        public override string GetFormattedResult()
        {
            return $"{this.TrackingId};{this.BatchId};{this.SequenceNumber};{this.Result}";
        }
    }
}
