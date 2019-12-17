// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System;
    using TestResultCoordinator.Storage;

    class TestReportGeneratorFactory : ITestReportGeneratorFactory
    {
        ITestOperationResultStorage storage;

        public TestReportGeneratorFactory(ITestOperationResultStorage storage)
        {
            this.storage = storage;
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
                            this.storage.GetStoreFromSource(reportMetadata.ExpectedSource),
                            reportMetadata.ActualSource,
                            this.storage.GetStoreFromSource(reportMetadata.ActualSource),
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
