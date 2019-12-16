// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System;
    using TestResultCoordinator.Storage;

    class TestReportGeneratorFactory : ITestReportGeneratorFactory
    {
        public TestReportGeneratorFactory()
        {
        }

        public ITestResultReportGenerator Create(
            string trackingId,
            IReportMetadata reportMetadata,
            ITestOperationResultStorage storage)
        {
            switch (reportMetadata.TestReportType)
            {
                case TestReportType.CountingReport:
                {
                        return new CountingReportGenerator(
                            trackingId,
                            reportMetadata.ExpectedSource,
                            storage.GetStoreFromSource(reportMetadata.ExpectedSource),
                            reportMetadata.ActualSource,
                            storage.GetStoreFromSource(reportMetadata.ActualSource),
                            reportMetadata.TestOperationResultType.ToString(),
                            new SimpleTestOperationResultComparer());
                }

                default:
                {
                        throw new NotSupportedException($"Report type {reportMetadata.TestReportType} is not supported.");
                }
            }
        }
    }
}
