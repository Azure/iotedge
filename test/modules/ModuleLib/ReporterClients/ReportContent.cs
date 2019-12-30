// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.ReporterClients
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class ReportContent
    {
        public Guid BatchId {get; private set;}        
        public string ResultMessage {get; private set;}
        public long SequenceNumber {get; private set;}
        public TestOperationResultType TestOperationResultType {get; private set;}
        public string TrackingId {get; private set;}


        public ReportContent SetBatchId(Guid batchId)
        {
            this.BatchId = batchId;
            return this;
        }

        public ReportContent SetResultMessage(string resultMessage)
        {
            this.ResultMessage = resultMessage;
            return this;
        }
        public ReportContent SetSequenceNumber(long sequenceNumber) 
        {
            this.SequenceNumber = sequenceNumber;
            return this;
        }

        public ReportContent SetTestOperationResultType(TestOperationResultType testOperationResultType)
        {
            this.TestOperationResultType = testOperationResultType;
            return this;
        }

        public ReportContent SetTrackingId(string trackingId)
        {
            this.TrackingId = trackingId;
            return this;
        }

        public Object GenerateReport() => GenerateReport(this.TestOperationResultType);
        public Object GenerateReport(TestOperationResultType testOperationResultType)
        {
            Preconditions.CheckNotNull(testOperationResultType, nameof(testOperationResultType));

            // TODO: Add the formatting for other type of reports
            switch (testOperationResultType)
            {
                // Send to TestResultCoordinator endpoint
                // Note: the `Source` and `CreatedAt` will be generated ReportingClient
                case TestOperationResultType.DirectMethod:
                    return
                        new Microsoft.Azure.Devices.Edge.ModuleUtil.TestResultCoordinatorClient.TestOperationResult
                        {
                            CreatedAt = DateTime.UtcNow,
                            Result = $"{this.TrackingId};{this.BatchId.ToString()};{this.SequenceNumber.ToString()};{this.ResultMessage}",
                            Type = Enum.GetName(typeof(TestOperationResultType), testOperationResultType)
                        };

                // Send to TestAnalyzer endpoint
                case TestOperationResultType.LegacyDirectMethod:
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