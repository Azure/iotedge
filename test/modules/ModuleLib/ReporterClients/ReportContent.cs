// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.ReporterClients
{
    using System;

    public sealed class ReportContent
    {
        public string TrackingId {get; private set;}
        public string BatchId {get; private set;}
        public string SequenceNumber {get; private set;}
        public string ResultMessage {get; private set;}

        public void SetTrackingId(string trackingId) => this.TrackingId = trackingId;
        public void SetBatchId(string batchId) => this.BatchId = batchId;
        public void SetSequenceNumber(string sequenceNumber) => this.SequenceNumber = sequenceNumber;
        public void SetResultMessage(string resultMessage) => this.ResultMessage = resultMessage;

        public Object GenerateReport(Object testOperationResultType)
        {
            // TODO: Add the formatting for other type of reports
            switch (testOperationResultType)
            {
                case TestOperationResultType.DirectMethod:
                // Send to TestResultCoordinator endpoint
                    return
                        new Microsoft.Azure.Devices.Edge.ModuleUtil.TestResultCoordinatorClient.TestOperationResult
                        {
                            CreatedAt = DateTime.UtcNow,
                            Result = $"{this.TrackingId};{this.BatchId};{this.SequenceNumber};{this.ResultMessage}",
                            Type = Enum.GetName(typeof(TestOperationResultType), testOperationResultType)
                        };

                case TestOperationResultType.LegacyDirectMethod:
                // Send to TestAnalyzer endpoint
                default:
                    return
                        new Microsoft.Azure.Devices.Edge.ModuleUtil.TestOperationResult
                        {
                            CreatedAt = DateTime.UtcNow,
                            Result = this.ResultMessage,
                            Type = Enum.GetName(typeof(TestOperationResultType), testOperationResultType)
                        };
            }
        }
    }
}