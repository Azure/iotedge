// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Storage;

    class TestReportGeneratorFactory : ITestReportGeneratorFactory
    {
        const int BatchSize = 500;
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
                    var expectedTestResults = new StoreTestResultCollection<TestOperationResult>(
                        this.storage.GetStoreFromSource(reportMetadata.ExpectedSource),
                        500);

                    var actualTestResults = new StoreTestResultCollection<TestOperationResult>(
                        this.storage.GetStoreFromSource(reportMetadata.ActualSource),
                        500);

                    return new CountingReportGenerator(
                        trackingId,
                        reportMetadata.ExpectedSource,
                        expectedTestResults,
                        reportMetadata.ActualSource,
                        actualTestResults,
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
