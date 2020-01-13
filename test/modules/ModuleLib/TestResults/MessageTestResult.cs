// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;

    public class MessageTestResult : TestResultBase
    {
        public MessageTestResult(string source, DateTime createdAt)
            : base(source, TestOperationResultType.Messages, createdAt)
        {
        }

        public string TrackingId { get; set; }

        public string BatchId { get; set; }

        public string SequenceNumber { get; set; }

        public override string GetFormattedResult()
        {
            return $"{this.TrackingId};{this.BatchId};{this.SequenceNumber}";
        }
    }
}
