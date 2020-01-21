// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using TestResultCoordinator.Reports;

    class DirectMethodReportMetadata : ITestReportMetadata
    {
        public DirectMethodReportMetadata(string senderSource, TestReportType testReportType, TimeSpan tolerancePeriod, string receiverSource = "")
        {
            this.SenderSource = senderSource;
            this.TestReportType = testReportType;
            this.TolerancePeriod = tolerancePeriod;
            if (receiverSource is null || string.Empty.Equals(receiverSource))
            {
                this.ReceiverSource = Option.None<string>();
            }
            else
            {
                this.ReceiverSource = Option.Some(receiverSource);
            }
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
