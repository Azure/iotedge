// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    class ReportGeneratorFactory : IReportGeneratorFactory
    {
        public ReportGeneratorFactory()
        {
        }

        public ITestResultReportGenerator Create(
            string trackingId,
            IReportMetadata reportMetadata)
        {
            switch (reportMetadata.ReportType)
            {
                case ReportType.CountingReport:
                {
                        return new CountingReportGenerator(
                            trackingId,
                            reportMetadata.ExpectedSource,
                            // TODO: Change the storage to be passed in when it becomes non-static
                            TestOperationResultStorage.GetStoreFromSource(reportMetadata.ExpectedSource),
                            reportMetadata.ActualSource,
                            TestOperationResultStorage.GetStoreFromSource(reportMetadata.ActualSource),
                            reportMetadata.TestOperationResultType.ToString(),
                            new SimpleTestOperationResultComparer());
                }

                default:
                {
                        throw new NotSupportedException($"Report type {reportMetadata.ReportType} is not supported.");
                }
            }
        }
    }
}
