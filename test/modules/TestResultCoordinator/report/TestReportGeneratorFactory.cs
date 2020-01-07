// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
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
                    var expectedTestResults = this.GetExpectedResults(reportMetadata);
                    var actualTestResults = this.GetActualResults(reportMetadata);

                    return new CountingReportGenerator(
                        trackingId,
                        reportMetadata.ExpectedSource,
                        expectedTestResults,
                        reportMetadata.ActualSource,
                        actualTestResults,
                        reportMetadata.TestOperationResultType.ToString(),
                        new SimpleTestOperationResultComparer());
                }

                case TestReportType.TwinCountingReport:
                {
                    var expectedTestResults = this.GetTwinExpectedResults(reportMetadata);
                    var actualTestResults = this.GetActualResults(reportMetadata);

                    return new TwinCountingReportGenerator(
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

        ITestResultCollection<TestOperationResult> GetActualResults(IReportMetadata reportMetadata)
        {
            return new StoreTestResultCollection<TestOperationResult>(
                this.storage.GetStoreFromSource(reportMetadata.ActualSource),
                BatchSize);
        }

        ITestResultCollection<TestOperationResult> GetExpectedResults(IReportMetadata reportMetadata)
        {
            return new StoreTestResultCollection<TestOperationResult>(
                this.storage.GetStoreFromSource(reportMetadata.ExpectedSource),
                BatchSize);
        }

        ITestResultCollection<TestOperationResult> GetTwinExpectedResults(IReportMetadata reportMetadata)
        {
            TwinCountingReportMetadata twinMetadata = reportMetadata as TwinCountingReportMetadata;
            if (twinMetadata == null)
            {
                throw new NotSupportedException($"Report type {reportMetadata.TestReportType} requires TwinReportMetadata instead of {reportMetadata.GetType()}");
            }

            if (twinMetadata.TwinTestPropertyType == TwinTestPropertyType.Reported)
            {
                return this.GetExpectedResults(reportMetadata);
            }

            string[] sources = reportMetadata.ExpectedSource.Split('.');
            string moduleId = sources.Length > 0 ? sources[0] : Settings.Current.ModuleId;
            return new CloudTwinTestResultCollection(reportMetadata.ExpectedSource, Settings.Current.ServiceClientConnectionString, moduleId, Settings.Current.TrackingId);
        }
    }
}
