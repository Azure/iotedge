// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::TestResultCoordinator;
    using global::TestResultCoordinator.Reports;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class TestReportHelperTest
    {
        [Fact]
        public async Task TestGenerateTestResultReportsAsync_MissingTrackingId()
        {
            var mockLogger = new Mock<ILogger>();
            var mockTestReportGeneratorFactory = new Mock<ITestReportGeneratorFactory>();

            ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                 TestReportHelper.GenerateTestResultReportsAsync(
                     string.Empty,
                     new List<IReportMetadata>(),
                     mockTestReportGeneratorFactory.Object,
                     mockLogger.Object));

            Assert.StartsWith("trackingId", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task TestGenerateTestResultReportsAsync_NullReportMetadata()
        {
            var mockLogger = new Mock<ILogger>();
            var mockTestReportGeneratorFactory = new Mock<ITestReportGeneratorFactory>();

            ArgumentNullException ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                 TestReportHelper.GenerateTestResultReportsAsync(
                     "fakeTrackngId",
                     null,
                     mockTestReportGeneratorFactory.Object,
                     mockLogger.Object));

            Assert.Equal("reportMetadatalist", ex.ParamName);
        }

        [Theory]
        [InlineData(false, false, false, 3)]
        [InlineData(true, false, true, 1)]
        [InlineData(true, false, false, 2)]
        [InlineData(true, true, true, 0)]
        public async Task TestGenerateTestResultReportsAsync_ReportGeneration(bool throwExceptionForTestReport1, bool throwExceptionForTestReport2, bool throwExceptionForTestReport3, int expectedReportCount)
        {
            var mockLogger = new Mock<ILogger>();
            var mockTestReportGeneratorFactory = new Mock<ITestReportGeneratorFactory>();

            string trackingId = "fakeTrackingId";
            var countingReportMetadata = new CountingReportMetadata("CountingExpectedSource", "CountingAcutalSource", TestOperationResultType.Messages, TestReportType.CountingReport);
            var twinCountingReportMetadata = new TwinCountingReportMetadata("TwinExpectedSource", "TwinActualSource", TestReportType.TwinCountingReport, TwinTestPropertyType.Desired);
            var deploymentReportMetadata = new DeploymentTestReportMetadata("DeploymentExpectedSource", "DeploymentActualSource");

            var mockTestReportGenerator1 = new Mock<ITestResultReportGenerator>();
            mockTestReportGenerator1.Setup(g => g.CreateReportAsync()).Returns(this.MockTestResultReport(throwExceptionForTestReport1));

            var mockTestReportGenerator2 = new Mock<ITestResultReportGenerator>();
            mockTestReportGenerator2.Setup(g => g.CreateReportAsync()).Returns(this.MockTestResultReport(throwExceptionForTestReport2));

            var mockTestReportGenerator3 = new Mock<ITestResultReportGenerator>();
            mockTestReportGenerator3.Setup(g => g.CreateReportAsync()).Returns(this.MockTestResultReport(throwExceptionForTestReport3));

            mockTestReportGeneratorFactory.Setup(f => f.Create(trackingId, countingReportMetadata)).Returns(mockTestReportGenerator1.Object);
            mockTestReportGeneratorFactory.Setup(f => f.Create(trackingId, twinCountingReportMetadata)).Returns(mockTestReportGenerator2.Object);
            mockTestReportGeneratorFactory.Setup(f => f.Create(trackingId, deploymentReportMetadata)).Returns(mockTestReportGenerator3.Object);

            ITestResultReport[] reports = await TestReportHelper.GenerateTestResultReportsAsync(
                trackingId,
                new List<IReportMetadata>
                {
                    countingReportMetadata,
                    twinCountingReportMetadata,
                    deploymentReportMetadata
                },
                mockTestReportGeneratorFactory.Object,
                mockLogger.Object);

            Assert.Equal(expectedReportCount, reports.Length);
        }

        private Task<ITestResultReport> MockTestResultReport(bool throwException)
        {
            if (!throwException)
            {
                return Task.FromResult<ITestResultReport>(new CountingReport("mock", "mock", "mock", "mock", 23, 21, 12, new List<TestOperationResult>()));
            }

            return Task.FromException<ITestResultReport>(new ApplicationException("Inject exception for testing"));
        }
    }
}
