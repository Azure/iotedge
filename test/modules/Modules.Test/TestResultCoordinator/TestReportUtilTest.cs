// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::TestResultCoordinator;
    using global::TestResultCoordinator.Reports;
    using global::TestResultCoordinator.Reports.DirectMethod;
    using global::TestResultCoordinator.Reports.DirectMethod.Connectivity;
    using global::TestResultCoordinator.Reports.DirectMethod.LongHaul;
    using global::TestResultCoordinator.Reports.EdgeHubRestartTest;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    [Unit]
    public class TestReportUtilTest
    {
        public static readonly string TestDescription = "dummy description";

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
        [InlineData(true, true, true, true, true, true, true, true, true, 0)]
        [InlineData(false, false, true, true, true, true, true, true, true, 2)]
        [InlineData(true, true, true, true, false, true, true, false, false, 3)]
        [InlineData(true, false, true, false, true, true, true, false, true, 3)]
        [InlineData(true, false, false, false, false, true, true, false, false, 6)]
        [InlineData(false, false, false, false, false, true, true, false, false, 7)]
        [InlineData(false, false, false, false, false, false, false, false, false, 9)]

        public async Task TestGenerateTestResultReportsAsync_ReportGeneration(
            bool throwExceptionForTestReport1,
            bool throwExceptionForTestReport2,
            bool throwExceptionForTestReport3,
            bool throwExceptionForTestReport4,
            bool throwExceptionForTestReport5,
            bool throwExceptionForTestReport6,
            bool throwExceptionForTestReport7,
            bool throwExceptionForTestReport8,
            bool throwExceptionForTestReport9,
            int expectedReportCount)
        {
            var mockLogger = new Mock<ILogger>();
            var mockTestReportGeneratorFactory = new Mock<ITestReportGeneratorFactory>();

            string trackingId = "fakeTrackingId";
            var countingReportMetadata = new CountingReportMetadata(TestDescription, "CountingExpectedSource", "CountingAcutalSource", TestOperationResultType.Messages, TestReportType.CountingReport, false);
            var twinCountingReportMetadata = new TwinCountingReportMetadata(TestDescription, "TwinExpectedSource", "TwinActualSource", TestReportType.TwinCountingReport, TwinTestPropertyType.Desired);
            var deploymentReportMetadata = new DeploymentTestReportMetadata(TestDescription, "DeploymentExpectedSource", "DeploymentActualSource");
            var directMethodConnectivityReportMetadata = new DirectMethodConnectivityReportMetadata(TestDescription, "DirectMethodSenderSource", new TimeSpan(0, 0, 0, 0, 5), "DirectMethodReceiverSource");
            var directMethodConnectivityReportMetadataWithoutReceiverSource = new DirectMethodConnectivityReportMetadata(TestDescription, "DirectMethodSenderSource", new TimeSpan(0, 0, 0, 0, 5), "DirectMethodReceiverSource");
            var directMethodLongHaulReportMetadata = new DirectMethodLongHaulReportMetadata(TestDescription, "DirectMethodSenderSource", "DirectMethodReceiverSource");
            var directMethodLongHaulReportMetadataWithoutReceiverSource = new DirectMethodLongHaulReportMetadata(TestDescription, "DirectMethodSenderSource", "DirectMethodReceiverSource");
            var edgeHubRestartMessageReportMetadata = new EdgeHubRestartMessageReportMetadata(TestDescription, "edgeHubRestartTester1.EdgeHubRestartMessage", "relayer1.receive");
            var edgeHubRestartDirectMethodReportMetadata = new EdgeHubRestartDirectMethodReportMetadata(TestDescription, "edgeHubRestartTester1.EdgeHubRestartDirectMethod", "directMethodReceiver1.receive");

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

            var mockTestReportGenerator6 = new Mock<ITestResultReportGenerator>();
            mockTestReportGenerator6.Setup(g => g.CreateReportAsync()).Returns(this.MockTestResultReport(throwExceptionForTestReport6));

            var mockTestReportGenerator7 = new Mock<ITestResultReportGenerator>();
            mockTestReportGenerator7.Setup(g => g.CreateReportAsync()).Returns(this.MockTestResultReport(throwExceptionForTestReport7));

            var mockTestReportGenerator8 = new Mock<ITestResultReportGenerator>();
            mockTestReportGenerator8.Setup(g => g.CreateReportAsync()).Returns(this.MockTestResultReport(throwExceptionForTestReport8));

            var mockTestReportGenerator9 = new Mock<ITestResultReportGenerator>();
            mockTestReportGenerator9.Setup(g => g.CreateReportAsync()).Returns(this.MockTestResultReport(throwExceptionForTestReport9));

            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, countingReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator1.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, twinCountingReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator2.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, deploymentReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator3.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, directMethodConnectivityReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator4.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, directMethodConnectivityReportMetadataWithoutReceiverSource)).Returns(Task.FromResult(mockTestReportGenerator5.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, directMethodLongHaulReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator6.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, directMethodLongHaulReportMetadataWithoutReceiverSource)).Returns(Task.FromResult(mockTestReportGenerator7.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, edgeHubRestartMessageReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator8.Object));
            mockTestReportGeneratorFactory.Setup(f => f.CreateAsync(trackingId, edgeHubRestartDirectMethodReportMetadata)).Returns(Task.FromResult(mockTestReportGenerator9.Object));

            ITestResultReport[] reports = await TestReportUtil.GenerateTestResultReportsAsync(
                trackingId,
                new List<ITestReportMetadata>
                {
                    countingReportMetadata,
                    twinCountingReportMetadata,
                    deploymentReportMetadata,
                    directMethodConnectivityReportMetadata,
                    directMethodConnectivityReportMetadataWithoutReceiverSource,
                    directMethodLongHaulReportMetadata,
                    directMethodLongHaulReportMetadataWithoutReceiverSource,
                    edgeHubRestartMessageReportMetadata,
                    edgeHubRestartDirectMethodReportMetadata
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
                        ""TestDescription"": ""messages | local | amqp"",
                        ""TestReportType"": ""CountingReport"",
                        ""TestOperationResultType"": ""Messages"",
                        ""ExpectedSource"": ""loadGen1.send"",
                        ""ActualSource"": ""relayer1.receive""
                    },
                    ""reportMetadata2"": {
                        ""TestDescription"": ""twin | desired property | amqp"",
                        ""TestReportType"": ""TwinCountingReport"",
                        ""TwinTestPropertyType"": ""Desired"",
                        ""ExpectedSource"": ""twinTester1.desiredUpdated"",
                        ""ActualSource"": ""twinTester2.desiredReceived""
                    },
                    ""reportMetadata3"": {
                        ""TestDescription"": ""deployment"",
                        ""TestReportType"": ""DeploymentTestReport"",
                        ""ExpectedSource"": ""deploymentTester1.send"",
                        ""ActualSource"": ""deploymentTester2.receive""
                    },
                    ""reportMetadata4"": {
                        ""TestDescription"": ""direct method | cloud | amqp"",
                        ""TestReportType"": ""DirectMethodConnectivityReport"",
                        ""SenderSource"": ""senderSource1.send"",
                        ""ReceiverSource"": ""receiverSource1.receive"",
                        ""TolerancePeriod"": ""00:00:00.005""
                    },
                    ""reportMetadata5"": {
                        ""TestDescription"": ""edge agent ping"",
                        ""TestReportType"": ""DirectMethodConnectivityReport"",
                        ""SenderSource"": ""senderSource1.send"",
                        ""TolerancePeriod"": ""00:00:00.005""
                    },
                    ""reportMetadata6"": {
                        ""TestDescription"": ""messages | local | amqp | restart"",
                        ""TestReportType"": ""EdgeHubRestartMessageReport"",
                        ""TestOperationResultType"": ""EdgeHubRestartMessage"",
                        ""SenderSource"": ""edgeHubRestartTester1.EdgeHubRestartMessage"",
                        ""ReceiverSource"": ""relayer1.receive""
                    },
                    ""reportMetadata7"": {
                        ""TestDescription"": ""direct method | cloud | amqp | restart"",
                        ""TestReportType"": ""EdgeHubRestartDirectMethodReport"",
                        ""TestOperationResultType"": ""EdgeHubRestartDirectMethod"",
                        ""SenderSource"": ""edgeHubRestartTester1.EdgeHubRestartDirectMethod"",
                        ""ReceiverSource"": ""directMethodReceiver1.receive""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Equal(7, results.Count);
        }

        [Fact]
        public void ParseReportMetadataList_ParseCountingReportMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestDescription"": ""messages | local | amqp"",
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
            Assert.Equal("messages | local | amqp", reportMetadata.TestDescription);
            Assert.Equal(TestOperationResultType.Messages, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.CountingReport, reportMetadata.TestReportType);
            Assert.Equal("loadGen1.send", reportMetadata.ExpectedSource);
            Assert.Equal("relayer1.receive", reportMetadata.ActualSource);
            Assert.False(reportMetadata.LongHaulEventHubMode);
        }

        [Fact]
        public void ParseReportMetadataList_ParseCountingReportEventHubMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestDescription"": ""messages | local | amqp"",
                        ""TestReportType"": ""CountingReport"",
                        ""LongHaulEventHubMode"": ""true"",
                        ""TestOperationResultType"": ""Messages"",
                        ""ExpectedSource"": ""loadGen1.send"",
                        ""ActualSource"": ""relayer1.receive""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as CountingReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal("messages | local | amqp", reportMetadata.TestDescription);
            Assert.Equal(TestOperationResultType.Messages, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.CountingReport, reportMetadata.TestReportType);
            Assert.Equal("loadGen1.send", reportMetadata.ExpectedSource);
            Assert.Equal("relayer1.receive", reportMetadata.ActualSource);
            Assert.True(reportMetadata.LongHaulEventHubMode);
        }

        [Fact]
        public void ParseReportMetadataList_ParseTwinCountingReportMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestDescription"": ""twin | desired property | amqp"",
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
            Assert.Equal("twin | desired property | amqp", reportMetadata.TestDescription);
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
                        ""TestDescription"": ""deployment"",
                        ""TestReportType"": ""DeploymentTestReport"",
                        ""ExpectedSource"": ""deploymentTester1.send"",
                        ""ActualSource"": ""deploymentTester2.receive""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as DeploymentTestReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal("deployment", reportMetadata.TestDescription);
            Assert.Equal(TestOperationResultType.Deployment, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.DeploymentTestReport, reportMetadata.TestReportType);
            Assert.Equal("deploymentTester1.send", reportMetadata.ExpectedSource);
            Assert.Equal("deploymentTester2.receive", reportMetadata.ActualSource);
        }

        [Fact]
        public void ParseReportMetadataList_ParseDirectMethodConnectivityTestReportMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestDescription"": ""direct method | cloud | amqp"",
                        ""TestReportType"": ""DirectMethodConnectivityReport"",
                        ""SenderSource"": ""directMethodSender1.send"",
                        ""ReceiverSource"": ""directMethodReceiver1.receive"",
                        ""TolerancePeriod"": ""00:00:00.005""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as DirectMethodConnectivityReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal("direct method | cloud | amqp", reportMetadata.TestDescription);
            Assert.Equal(TestOperationResultType.DirectMethod, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.DirectMethodConnectivityReport, reportMetadata.TestReportType);
            Assert.Equal("directMethodSender1.send", reportMetadata.SenderSource);
            Assert.True(reportMetadata.ReceiverSource.HasValue);
            reportMetadata.ReceiverSource.ForEach(x => Assert.Equal("directMethodReceiver1.receive", x));
            Assert.Equal(new TimeSpan(0, 0, 0, 0, 5), reportMetadata.TolerancePeriod);
        }

        [Fact]
        public void ParseReportMetadataList_ParseDirectMethodLongHaulTestReportMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestDescription"": ""direct method | cloud | amqp"",
                        ""TestReportType"": ""DirectMethodLongHaulReport"",
                        ""SenderSource"": ""directMethodSender1.send"",
                        ""ReceiverSource"": ""directMethodReceiver1.receive""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as DirectMethodLongHaulReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal("direct method | cloud | amqp", reportMetadata.TestDescription);
            Assert.Equal(TestOperationResultType.DirectMethod, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.DirectMethodLongHaulReport, reportMetadata.TestReportType);
            Assert.Equal("directMethodSender1.send", reportMetadata.SenderSource);
            Assert.Equal("directMethodReceiver1.receive", reportMetadata.ReceiverSource);
        }

        [Fact]
        public void ParseReportMetadataList_ParseDirectMethodConnectivityTestReportMetadataWithoutReceiverSource()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestDescription"": ""edge agent ping"",
                        ""TestReportType"": ""DirectMethodConnectivityReport"",
                        ""SenderSource"": ""directMethodSender1.send"",
                        ""TolerancePeriod"": ""00:00:00.005""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as DirectMethodConnectivityReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal("edge agent ping", reportMetadata.TestDescription);
            Assert.Equal(TestOperationResultType.DirectMethod, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.DirectMethodConnectivityReport, reportMetadata.TestReportType);
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
                        ""TestDescription"": ""network controller"",
                        ""TestReportType"": ""NetworkControllerReport"",
                        ""Source"": ""networkController""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as NetworkControllerReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal("network controller", reportMetadata.TestDescription);
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
                        ""TestDescription"": ""error"",
                        ""TestReportType"": ""ErrorReport""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as ErrorReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal("error", reportMetadata.TestDescription);
            Assert.Equal(TestOperationResultType.Error, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.ErrorReport, reportMetadata.TestReportType);
            Assert.Equal(TestConstants.Error.TestResultSource, reportMetadata.Source);
        }

        [Fact]
        public void ParseReportMetadataList_ParseEdgeHubRestartMessageReportMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestDescription"": ""messages | local | amqp"",
                        ""TestReportType"": ""EdgeHubRestartMessageReport"",
                        ""TestOperationResultType"": ""EdgeHubRestartMessage"",
                        ""SenderSource"": ""edgeHubRestartTester1.EdgeHubRestartMessage"",
                        ""ReceiverSource"": ""relayer1.receive""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as EdgeHubRestartMessageReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal("messages | local | amqp", reportMetadata.TestDescription);
            Assert.Equal(TestOperationResultType.EdgeHubRestartMessage, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.EdgeHubRestartMessageReport, reportMetadata.TestReportType);
            Assert.Equal("edgeHubRestartTester1.EdgeHubRestartMessage", reportMetadata.SenderSource);
            Assert.Equal("relayer1.receive", reportMetadata.ReceiverSource);
        }

        [Fact]
        public void ParseReportMetadataList_ParseEdgeHubRestartDirectMethodReportMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata7"": {
                        ""TestDescription"": ""direct method | cloud | amqp"",
                        ""TestReportType"": ""EdgeHubRestartDirectMethodReport"",
                        ""TestOperationResultType"": ""EdgeHubRestartDirectMethod"",
                        ""SenderSource"": ""edgeHubRestartTester1.EdgeHubRestartDirectMethod"",
                        ""ReceiverSource"": ""directMethodReceiver1.receive""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as EdgeHubRestartDirectMethodReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal("direct method | cloud | amqp", reportMetadata.TestDescription);
            Assert.Equal(TestOperationResultType.EdgeHubRestartDirectMethod, reportMetadata.TestOperationResultType);
            Assert.Equal(TestReportType.EdgeHubRestartDirectMethodReport, reportMetadata.TestReportType);
            Assert.Equal("edgeHubRestartTester1.EdgeHubRestartDirectMethod", reportMetadata.SenderSource);
            Assert.Equal("directMethodReceiver1.receive", reportMetadata.ReceiverSource);
        }

        [Fact]
        public void ParseReportMetadataList_ParseTestInfoReportMetadata()
        {
            const string testDataJson =
                @"{
                    ""reportMetadata"": {
                        ""TestDescription"": ""test info"",
                        ""TestReportType"": ""TestInfoReport""
                    }
                }";

            List<ITestReportMetadata> results = TestReportUtil.ParseReportMetadataJson(testDataJson, new Mock<ILogger>().Object);

            Assert.Single(results);
            var reportMetadata = results[0] as TestInfoReportMetadata;
            Assert.NotNull(reportMetadata);
            Assert.Equal("test info", reportMetadata.TestDescription);
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
                        ""TestDescription"": ""dummy"",
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
                return Task.FromResult<ITestResultReport>(new CountingReport("mock", "mock", "mock", "mock", "mock", 23, 21, 12, new List<TestOperationResult>(), Option.None<EventHubSpecificReportComponents>(), Option.None<DateTime>()));
            }

            return Task.FromException<ITestResultReport>(new ApplicationException("Inject exception for testing"));
        }
    }
}
