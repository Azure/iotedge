// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;

    public class DirectMethodTestResult : TestResultBase
    {
        public DirectMethodTestResult(string source, DateTime createdAt) :
            base(source, TestOperationResultType.DirectMethod, createdAt)
        {
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
