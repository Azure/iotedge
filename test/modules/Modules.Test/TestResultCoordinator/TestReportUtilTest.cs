// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::TestResultCoordinator;
    using global::TestResultCoordinator.Reports;
    using global::TestResultCoordinator.Reports.DirectMethod;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class TestReportUtilTest
    {
        [Fact]
        public async Task TestGenerateTestResultReportsAsync_MissingTrackingId()
        {
            var mockLogger = new Mock<ILogger>();
            var mockTestReportGeneratorFactory = new Mock<ITestReportGeneratorFactory>();

            ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                 TestReportUtil.GenerateTestResultReportsAsync(
                     string.Empty,
                     new List<ITestReportMetadata>(),
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
                 TestReportUtil.GenerateTestResultReportsAsync(
                     "fakeTrackingId",
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

            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, countingReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator1.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, twinCountingReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator2.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, deploymentReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator3.Object));

            ITestResultReport[] reports = await TestReportUtil.GenerateTestResultReportsAsync(
                trackingId,
                new List<ITestReportMetadata>
                {
                    countingReportMetadata,
                    twinCountingReportMetadata,
                    deploymentReportMetadata
                },
                mockTestReportGeneratorFactory.Object,
                mockLogger.Object);

            Assert.Equal(expectedReportCount, reports.Length);
        }

        [Fact]
        public void ParseReportMetadataList_MultipleReportMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata1"": {
                        ""TestReportType"": ""CountingReport"",
                        ""TestOperationResultType"": ""Messages"",
                        ""ExpectedSource"": ""loadGen1.send"",
                        ""ActualSource"": ""relayer1.receive""
                    },
                    ""reportMetadata2"": {
                        ""TestReportType"": ""TwinCountingReport"",
                        ""TwinTestPropertyType"": ""Desired"",
                        ""ExpectedSource"": ""twinTester1.desiredUpdated"",
                        ""ActualSource"": ""twinTester2.desiredReceived""
                    },
                    ""reportMetadata3"": {
                        ""TestReportType"": ""DeploymentTestReport"",
                        ""ExpectedSource"": ""deploymentTester1.send"",
                        ""ActualSource"": ""deploymentTester2.receive""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Equal(3, results.Count);
        }

        [Fact]
        public void ParseReportMetadataList_ParseCountingReport()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestReportType"": ""CountingReport"",
                        ""TestOperationResultType"": ""Messages"",
                        ""ExpectedSource"": ""loadGen1.send"",
                        ""ActualSource"": ""relayer1.receive""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as CountingReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal(TestOperationResultType.Messages, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.CountingReport, reportMetadata.TestReportType);
            Assert.Equal("loadGen1.send", reportMetadata.ExpectedSource);
            Assert.Equal("relayer1.receive", reportMetadata.ActualSource);
        }

        [Fact]
        public void ParseReportMetadataList_ParseTwinCountingReport()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestReportType"": ""TwinCountingReport"",
                        ""TwinTestPropertyType"": ""Desired"",
                        ""ExpectedSource"": ""twinTester1.desiredUpdated"",
                        ""ActualSource"": ""twinTester2.desiredReceived""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as TwinCountingReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal(TestOperationResultType.Twin, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.TwinCountingReport, reportMetadata.TestReportType);
            Assert.Equal(TwinTestPropertyType.Desired, reportMetadata.TwinTestPropertyType);
            Assert.Equal("twinTester1.desiredUpdated", reportMetadata.ExpectedSource);
            Assert.Equal("twinTester2.desiredReceived", reportMetadata.ActualSource);
        }

        [Fact]
        public void ParseReportMetadataList_ParseDeploymentTestReport()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestReportType"": ""DeploymentTestReport"",
                        ""ExpectedSource"": ""deploymentTester1.send"",
                        ""ActualSource"": ""deploymentTester2.receive""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as DeploymentTestReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal(TestOperationResultType.Deployment, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.DeploymentTestReport, reportMetadata.TestReportType);
            Assert.Equal("deploymentTester1.send", reportMetadata.ExpectedSource);
            Assert.Equal("deploymentTester2.receive", reportMetadata.ActualSource);
        }

        [Fact]
        public void ParseReportMetadataList_ParseDirectMethodTestReport()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestReportType"": ""DirectMethodReport"",
                        ""ExpectedSource"": ""directMethodSender1.send"",
                        ""ActualSource"": ""directMethodReceiver1.receive"",
                        ""TolerancePeriod"": ""00:00:00.005""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as DirectMethodReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal(TestOperationResultType.DirectMethod, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.DirectMethodReport, reportMetadata.TestReportType);
            Assert.Equal("directMethodSender1.send", reportMetadata.ExpectedSource);
            Assert.Equal("directMethodReceiver1.receive", reportMetadata.ActualSource);
            Assert.Equal(new TimeSpan(0, 0, 0, 0, 5), reportMetadata.TolerancePeriod);
        }

        [Fact]
        public void ParseReportMetadataList_ThrowExceptionWhenParseInvalidData()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestReportType"": ""TypeNotExist"",
                        ""ExpectedSource"": ""deploymentTester1.send"",
                        ""ActualSource"": ""deploymentTester2.receive""
                    }
                }";

            bool exceptionThrown = false;

            try
            {
                TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);
            }
            catch (Exception)
            {
                exceptionThrown = true;
            }

            Assert.True(exceptionThrown);
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
