// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    sealed class EdgeHubRestartTestReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(EdgeHubRestartTestReportGenerator));

        internal EdgeHubRestartTestReportGenerator(
            string trackingId,
            string restarterSource,
            ITestResultCollection<TestOperationResult> restartResults,
            ITestResultReport attachedTestReport)
        {
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.RestarterSource = Preconditions.CheckNonWhiteSpace(restarterSource, nameof(restarterSource));
            this.RestartResults = Preconditions.CheckNotNull(restartResults);
            this.AttachedTestReport = Preconditions.CheckNotNull(attachedTestReport);
        }

        internal string TrackingId { get; }

        internal string RestarterSource { get; set; }

        internal ITestResultCollection<TestOperationResult> RestartResults { get; set; }

        internal ITestResultReport AttachedTestReport { get; set; }

        public Task<ITestResultReport> CreateReportAsync()
        {
            // Verify the timestamp that uptime is over/under the time
            var senderTestResults = TestReportGeneratorFactory.GetResults(this.metadata.SenderSource);
            var receiverTestResults = metadata.ReceiverSource.Map(x => TestReportGeneratorFactory.GetResults(x));

            // Recurse to make sure the test is passing under its own rules
            
        }
    }


}