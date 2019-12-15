// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Storage;

    class TestReportGeneratorFactory : ITestReportGeneratorFactory
    {
        readonly ITestOperationResultStorage storage;

        public TestReportGeneratorFactory(ITestOperationResultStorage storage)
        {
            this.storage = Preconditions.CheckNotNull(storage, nameof(storage));
        }

        public ITestResultReportGenerator Create(
            string trackingId,
            IReportMetadata reportMetadata)
        {
            switch (reportMetadata.TestReportType)
            {
                case TestReportType.CountingReport:
                {
                    var expectedResultsLoader = new SimpleResults(
                        reportMetadata.ExpectedSource,
                        this.storage.GetStoreFromSource(reportMetadata.ExpectedSource),
                        500);

                    var actualResultsLoader = new SimpleResults(
                        reportMetadata.ExpectedSource,
                        this.storage.GetStoreFromSource(reportMetadata.ActualSource),
                        500);

                    return new CountingReportGenerator(
                        trackingId,
                        reportMetadata.ExpectedSource,
                        expectedResultsLoader,
                        reportMetadata.ActualSource,
                        actualResultsLoader,
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
