// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.DirectMethod;
    using TestResultCoordinator.Reports.DirectMethod.Connectivity;
    using TestResultCoordinator.Reports.DirectMethod.LongHaul;
    using TestResultCoordinator.Reports.EdgeHubRestartTest;
    using TestResultCoordinator.Reports.LegacyTwin;
    using TestResultCoordinator.Storage;

    class TestReportGeneratorFactory : ITestReportGeneratorFactory
    {
        const int BatchSize = 500;

        internal TestReportGeneratorFactory(ITestOperationResultStorage storage, NetworkControllerType networkControllerType, Option<LongHaulSpecificSettings> longhaulSettings)
        {
            this.Storage = Preconditions.CheckNotNull(storage, nameof(storage));
            this.NetworkControllerType = networkControllerType;
            this.LonghaulSettings = longhaulSettings;
        }

        ITestOperationResultStorage Storage { get; }

        NetworkControllerType NetworkControllerType { get; }

        Option<LongHaulSpecificSettings> LonghaulSettings { get; }

        public async Task<ITestResultReportGenerator> CreateAsync(
            string trackingId,
            ITestReportMetadata testReportMetadata)
        {
            Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            Preconditions.CheckNotNull(testReportMetadata, nameof(testReportMetadata));

            switch (testReportMetadata.TestReportType)
            {
                case TestReportType.CountingReport:
                    {
                        var metadata = (CountingReportMetadata)testReportMetadata;
                        var expectedTestResults = this.GetResults(metadata.ExpectedSource);
                        var actualTestResults = this.GetResults(metadata.ActualSource);

                        await this.LonghaulSettings.ForEachAsync(async (longhaulSettings) =>
                        {
                            TestResultFilter filter = new TestResultFilter(new SimpleTestOperationResultComparer());
                            TimeSpan unmatchedResultTolerance = longhaulSettings.UnmatchedResultTolerance;
                            (expectedTestResults, actualTestResults) = await filter.FilterResults(unmatchedResultTolerance, expectedTestResults, actualTestResults);
                        });

                        return new CountingReportGenerator(
                            metadata.TestDescription,
                            trackingId,
                            metadata.ExpectedSource,
                            expectedTestResults.GetAsyncEnumerator(),
                            metadata.ActualSource,
                            actualTestResults.GetAsyncEnumerator(),
                            testReportMetadata.TestOperationResultType.ToString(),
                            new SimpleTestOperationResultComparer(),
                            Settings.Current.UnmatchedResultsMaxSize,
                            metadata.LongHaulEventHubMode);
                    }

                case TestReportType.TwinCountingReport:
                    {
                        var metadata = (TwinCountingReportMetadata)testReportMetadata;
                        var expectedTestResults = this.GetTwinExpectedResults(metadata);
                        var actualTestResults = this.GetResults(metadata.ActualSource);

                        return new TwinCountingReportGenerator(
                            metadata.TestDescription,
                            trackingId,
                            metadata.ExpectedSource,
                            expectedTestResults.GetAsyncEnumerator(),
                            metadata.ActualSource,
                            actualTestResults.GetAsyncEnumerator(),
                            testReportMetadata.TestOperationResultType.ToString(),
                            new SimpleTestOperationResultComparer(),
                            Settings.Current.UnmatchedResultsMaxSize);
                    }

                case TestReportType.LegacyTwinReport:
                    {
                        var metadata = (LegacyTwinReportMetadata)testReportMetadata;
                        var testResults = this.GetResults(metadata.SenderSource);

                        return new LegacyTwinReportGenerator(
                            metadata.TestDescription,
                            trackingId,
                            testReportMetadata.TestOperationResultType.ToString(),
                            metadata.SenderSource,
                            testResults.GetAsyncEnumerator());
                    }

                case TestReportType.DeploymentTestReport:
                    {
                        var metadata = (DeploymentTestReportMetadata)testReportMetadata;
                        var expectedTestResults = this.GetResults(metadata.ExpectedSource);
                        var actualTestResults = this.GetResults(metadata.ActualSource);

                        return new DeploymentTestReportGenerator(
                            metadata.TestDescription,
                            trackingId,
                            metadata.ExpectedSource,
                            expectedTestResults.GetAsyncEnumerator(),
                            metadata.ActualSource,
                            actualTestResults.GetAsyncEnumerator(),
                            Settings.Current.UnmatchedResultsMaxSize);
                    }

                case TestReportType.DirectMethodConnectivityReport:
                    {
                        var metadata = (DirectMethodConnectivityReportMetadata)testReportMetadata;
                        var senderTestResults = this.GetResults(metadata.SenderSource);
                        var receiverTestResultsEnumerator = metadata.ReceiverSource.Map(x => this.GetResults(x).GetAsyncEnumerator());
                        var tolerancePeriod = metadata.TolerancePeriod;
                        var networkStatusTimeline = await this.GetNetworkStatusTimelineAsync(tolerancePeriod);

                        return new DirectMethodConnectivityReportGenerator(
                            metadata.TestDescription,
                            trackingId,
                            metadata.SenderSource,
                            senderTestResults.GetAsyncEnumerator(),
                            metadata.ReceiverSource,
                            receiverTestResultsEnumerator,
                            metadata.TestOperationResultType.ToString(),
                            networkStatusTimeline,
                            this.NetworkControllerType);
                    }

                case TestReportType.DirectMethodLongHaulReport:
                    {
                        var metadata = (DirectMethodLongHaulReportMetadata)testReportMetadata;
                        var senderTestResults = this.GetResults(metadata.SenderSource);
                        var receiverTestResults = this.GetResults(metadata.ReceiverSource);

                        await this.LonghaulSettings.ForEachAsync(async (longhaulSettings) =>
                        {
                            TestResultFilter filter = new TestResultFilter(new DirectMethodTestOperationResultComparer());
                            TimeSpan unmatchedResultTolerance = longhaulSettings.UnmatchedResultTolerance;
                            (senderTestResults, receiverTestResults) = await filter.FilterResults(unmatchedResultTolerance, senderTestResults, receiverTestResults);
                        });

                        return new DirectMethodLongHaulReportGenerator(
                            metadata.TestDescription,
                            trackingId,
                            metadata.SenderSource,
                            senderTestResults.GetAsyncEnumerator(),
                            metadata.ReceiverSource,
                            receiverTestResults.GetAsyncEnumerator(),
                            metadata.TestOperationResultType.ToString());
                    }

                case TestReportType.EdgeHubRestartDirectMethodReport:
                    {
                        var metadata = (EdgeHubRestartDirectMethodReportMetadata)testReportMetadata;
                        var senderTestResults = this.GetResults(metadata.SenderSource);
                        var receiverTestResults = this.GetResults(metadata.ReceiverSource);

                        return new EdgeHubRestartDirectMethodReportGenerator(
                            metadata.TestDescription,
                            trackingId,
                            metadata.SenderSource,
                            metadata.ReceiverSource,
                            metadata.TestReportType,
                            senderTestResults.GetAsyncEnumerator(),
                            receiverTestResults.GetAsyncEnumerator());
                    }

                case TestReportType.EdgeHubRestartMessageReport:
                    {
                        var metadata = (EdgeHubRestartMessageReportMetadata)testReportMetadata;
                        var senderTestResults = this.GetResults(metadata.SenderSource);
                        var receiverTestResults = this.GetResults(metadata.ReceiverSource);

                        return new EdgeHubRestartMessageReportGenerator(
                            metadata.TestDescription,
                            trackingId,
                            metadata.SenderSource,
                            metadata.ReceiverSource,
                            metadata.TestReportType,
                            senderTestResults.GetAsyncEnumerator(),
                            receiverTestResults.GetAsyncEnumerator());
                    }

                case TestReportType.NetworkControllerReport:
                    {
                        var metadata = (NetworkControllerReportMetadata)testReportMetadata;
                        var testResults = this.GetResults(metadata.Source);

                        return new SimpleReportGenerator(
                            metadata.TestDescription,
                            trackingId,
                            metadata.Source,
                            testResults.GetAsyncEnumerator(),
                            TestOperationResultType.Network);
                    }

                case TestReportType.ErrorReport:
                    {
                        var metadata = (ErrorReportMetadata)testReportMetadata;
                        var testResults = this.GetResults(metadata.Source);

                        return new SimpleReportGenerator(
                            metadata.TestDescription,
                            trackingId,
                            metadata.Source,
                            testResults.GetAsyncEnumerator(),
                            TestOperationResultType.Error);
                    }

                case TestReportType.TestInfoReport:
                    {
                        var metadata = (TestInfoReportMetadata)testReportMetadata;
                        var testResults = this.GetResults(metadata.Source);

                        return new SimpleReportGenerator(
                            metadata.TestDescription,
                            trackingId,
                            metadata.Source,
                            testResults.GetAsyncEnumerator(),
                            TestOperationResultType.TestInfo);
                    }

                default:
                    {
                        throw new NotSupportedException($"Report type {testReportMetadata.TestReportType} is not supported.");
                    }
            }
        }

        async Task<NetworkStatusTimeline> GetNetworkStatusTimelineAsync(TimeSpan tolerancePeriod)
        {
            IAsyncEnumerable<TestOperationResult> store = new StoreTestResultCollection<TestOperationResult>(this.Storage.GetStoreFromSource("networkController"), BatchSize);
            return await NetworkStatusTimeline.CreateAsync(
                store.GetAsyncEnumerator(),
                tolerancePeriod);
        }

        IAsyncEnumerable<TestOperationResult> GetResults(string resultSource)
        {
            return new StoreTestResultCollection<TestOperationResult>(
                this.Storage.GetStoreFromSource(resultSource),
                BatchSize);
        }

        IAsyncEnumerable<TestOperationResult> GetTwinExpectedResults(TwinCountingReportMetadata reportMetadata)
        {
            if (reportMetadata == null)
            {
                throw new NotSupportedException($"Report type {reportMetadata.TestReportType} requires TwinReportMetadata instead of {reportMetadata.GetType()}");
            }

            if (reportMetadata.TwinTestPropertyType == TwinTestPropertyType.Desired)
            {
                return this.GetResults(reportMetadata.ExpectedSource);
            }

            string[] sources = reportMetadata.ExpectedSource.Split('.');
            string moduleId = sources.Length > 0 ? sources[0] : Settings.Current.ModuleId;
            return new CloudTwinTestResultCollection(reportMetadata.ExpectedSource, Settings.Current.IoTHubConnectionString, moduleId, Settings.Current.TrackingId);
        }
    }
}
