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
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    [Unit]
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
        [InlineData(false, false, false, false, false, 5)]
        [InlineData(true, false, true, false, false, 3)]
        [InlineData(true, false, false, false, false, 4)]
        [InlineData(false, false, true, true, true, 2)]
        [InlineData(true, true, true, true, false, 1)]
        public async Task TestGenerateTestResultReportsAsync_ReportGeneration(
            bool throwExceptionForTestReport1,
            bool throwExceptionForTestReport2,
            bool throwExceptionForTestReport3,
            bool throwExceptionForTestReport4,
            bool throwExceptionForTestReport5,
            int expectedReportCount)
        {
            var mockLogger = new Mock<ILogger>();
            var mockTestReportGeneratorFactory = new Mock<ITestReportGeneratorFactory>();

            string trackingId = "fakeTrackingId";
            var countingReportMetadata = new CountingReportMetadata("CountingExpectedSource", "CountingAcutalSource", TestOperationResultType.Messages, TestReportType.CountingReport);
            var twinCountingReportMetadata = new TwinCountingReportMetadata("TwinExpectedSource", "TwinActualSource", TestReportType.TwinCountingReport, TwinTestPropertyType.Desired);
            var deploymentReportMetadata = new DeploymentTestReportMetadata("DeploymentExpectedSource", "DeploymentActualSource");
            var directMethodReportMetadata = new DirectMethodReportMetadata("DirectMethodSenderSource", new TimeSpan(0, 0, 0, 0, 5), "DirectMethodReceiverSource");
            var directMethodReportMetadataWithoutReceiverSource = new DirectMethodReportMetadata("DirectMethodSenderSource", new TimeSpan(0, 0, 0, 0, 5), "DirectMethodReceiverSource");

            var mockTestReportGenerator1 = new Mock<ITestResultReportGenerator>();
            mockTestReportGenerator1.Setup(g => g.CreateReportAsync()).Returns(this.MockTestResultReport(throwExceptionForTestReport1));

            var mockTestReportGenerator2 = new Mock<ITestResultReportGenerator>();
            mockTestReportGenerator2.Setup(g => g.CreateReportAsync()).Returns(this.MockTestResultReport(throwExceptionForTestReport2));

            var mockTestReportGenerator3 = new Mock<ITestResultReportGenerator>();
            mockTestReportGenerator3.Setup(g => g.CreateReportAsync()).Returns(this.MockTestResultReport(throwExceptionForTestReport3));

            var mockTestReportGenerator4 = new Mock<ITestResultReportGenerator>();
            mockTestReportGenerator4.Setup(g => g.CreateReportAsync()).Returns(this.MockTestResultReport(throwExceptionForTestReport4));

            var mockTestReportGenerator5 = new Mock<ITestResultReportGenerator>();
            mockTestReportGenerator5.Setup(g => g.CreateReportAsync()).Returns(this.MockTestResultReport(throwExceptionForTestReport5));

            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, countingReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator1.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, twinCountingReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator2.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, deploymentReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator3.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, directMethodReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator4.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, directMethodReportMetadataWithoutReceiverSource)).Returns(Task.FromResult(mockTestReportGenerator5.Object));

            ITestResultReport[] reports = await TestReportUtil.GenerateTestResultReportsAsync(
                trackingId,
                new List<ITestReportMetadata>
                {
                    countingReportMetadata,
                    twinCountingReportMetadata,
                    deploymentReportMetadata,
                    directMethodReportMetadata,
                    directMethodReportMetadataWithoutReceiverSource
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
                    },
                    ""reportMetadata4"": {
                        ""TestReportType"": ""DirectMethodReport"",
                        ""SenderSource"": ""senderSource1.send"",
                        ""ReceiverSource"": ""receiverSource1.receive"",
                        ""TolerancePeriod"": ""00:00:00.005""
                    },
                    ""reportMetadata5"": {
                        ""TestReportType"": ""DirectMethodReport"",
                        ""SenderSource"": ""senderSource1.send"",
                        ""TolerancePeriod"": ""00:00:00.005""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Equal(5, results.Count);
        }

        [Fact]
        public void ParseReportMetadataList_ParseCountingReportMetadata()
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
        public void ParseReportMetadataList_ParseTwinCountingReportMetadata()
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
        public void ParseReportMetadataList_ParseDeploymentTestReportMetadata()
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
        public void ParseReportMetadataList_ParseDirectMethodTestReportMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestReportType"": ""DirectMethodReport"",
                        ""SenderSource"": ""directMethodSender1.send"",
                        ""ReceiverSource"": ""directMethodReceiver1.receive"",
                        ""TolerancePeriod"": ""00:00:00.005""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as DirectMethodReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal(TestOperationResultType.DirectMethod, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.DirectMethodReport, reportMetadata.TestReportType);
            Assert.Equal("directMethodSender1.send", reportMetadata.SenderSource);
            Assert.True(reportMetadata.ReceiverSource.HasValue);
            reportMetadata.ReceiverSource.ForEach(x => Assert.Equal("directMethodReceiver1.receive", x));
            Assert.Equal(new TimeSpan(0, 0, 0, 0, 5), reportMetadata.TolerancePeriod);
        }

        [Fact]
        public void ParseReportMetadataList_ParseDirectMethodTestReportMetadataWithoutReceiverSource()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestReportType"": ""DirectMethodReport"",
                        ""SenderSource"": ""directMethodSender1.send"",
                        ""TolerancePeriod"": ""00:00:00.005""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as DirectMethodReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal(TestOperationResultType.DirectMethod, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.DirectMethodReport, reportMetadata.TestReportType);
            Assert.Equal("directMethodSender1.send", reportMetadata.SenderSource);
            Assert.False(reportMetadata.ReceiverSource.HasValue);
            Assert.Equal(new TimeSpan(0, 0, 0, 0, 5), reportMetadata.TolerancePeriod);
        }

        [Fact]
        public void ParseReportMetadataList_ParseNetworkControllerReportMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestReportType"": ""NetworkControllerReport"",
                        ""Source"": ""networkController""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as NetworkControllerReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal(TestOperationResultType.Network, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.NetworkControllerReport, reportMetadata.TestReportType);
            Assert.Equal("networkController", reportMetadata.Source);
        }

        [Fact]
        public void ParseReportMetadataList_ParseErrorReportMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestReportType"": ""ErrorReport""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as ErrorReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal(TestOperationResultType.Error, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.ErrorReport, reportMetadata.TestReportType);
            Assert.Equal(TestConstants.Error.TestResultSource, reportMetadata.Source);
        }

        [Fact]
        public void ParseReportMetadataList_ParseTestInfoReportMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestReportType"": ""TestInfoReport""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as TestInfoReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal(TestOperationResultType.TestInfo, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.TestInfoReport, reportMetadata.TestReportType);
            Assert.Equal(TestConstants.TestInfo.TestResultSource, reportMetadata.Source);
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
