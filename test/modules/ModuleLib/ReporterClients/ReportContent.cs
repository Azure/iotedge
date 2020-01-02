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
        public string Source {get; private set;}
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

        public ReportContent SetSource(string source) 
        {
            this.Source = source;
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
        public Object GenerateReport(TestOperationResultType resultFormat)
        {
            // TODO: Add the formatting rules for other type of reports
            switch (resultFormat)
            {
                // Send to TestResultCoordinator endpoint
                // Note: the `CreatedAt` will be generated ReportingClient
                case TestOperationResultType.DirectMethod:
                    return
                        new TestOperationResultDto
                        {
                            CreatedAt = DateTime.UtcNow,
                            Result = $"{this.TrackingId};{this.BatchId.ToString()};{this.SequenceNumber.ToString()};{this.ResultMessage}",
                            Source = $"{this.Source}",
                            Type = Enum.GetName(typeof(TestOperationResultType), resultFormat)
                        };

                // Send to TestAnalyzer endpoint
                case TestOperationResultType.LegacyDirectMethod:
                default:
                    return
                        new TestOperationResultDto
                        {
                            CreatedAt = DateTime.UtcNow,
                            Result = this.ResultMessage,
                            Source = $"{this.Source}",
                            Type = Enum.GetName(typeof(TestOperationResultType), resultFormat)
                        };
            }
        }
    }
}