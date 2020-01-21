// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Reports;

    class DirectMethodReportMetadata : ITestReportMetadata
    {
        public DirectMethodReportMetadata(string senderSource, TestReportType testReportType, TimeSpan tolerancePeriod)
        {
            this.SenderSource = senderSource;
            this.ReceiverSource = Option.None<string>();
            this.TestReportType = testReportType;
            this.TolerancePeriod = tolerancePeriod;
        }

        public DirectMethodReportMetadata(string senderSource, string receiverSource, TestReportType testReportType, TimeSpan tolerancePeriod)
        {
            this.SenderSource = senderSource;
            this.ReceiverSource = Option.Some(receiverSource);
            this.TestReportType = testReportType;
            this.TolerancePeriod = tolerancePeriod;
        }

        public TimeSpan TolerancePeriod { get; }

        public string SenderSource { get; }

        public Option<string> ReceiverSource { get; }

        public string[] ResultSources
        {
            get
            {
                List<string> resultSources = new List<string>();
                resultSources.Add(this.SenderSource);
                this.ReceiverSource.ForEach(x => resultSources.Add(x));
                return resultSources.ToArray();
            }
        }

        public TestReportType TestReportType { get; }

        public TestOperationResultType TestOperationResultType => TestOperationResultType.DirectMethod;
    }
}
