// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System;
    class TestReportGeneratorFactory : ITestReportGeneratorFactory
    {
        public TestReportGeneratorFactory()
        {
        }

        public ITestResultReportGenerator Create(
            string trackingId,
            IReportMetadata reportMetadata)
        {
            switch (reportMetadata.TestReportType)
            {
                case TestReportType.CountingReport:
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
                        throw new NotSupportedException($"Report type {reportMetadata.TestReportType} is not supported.");
                }
            }
        }
    }
}
