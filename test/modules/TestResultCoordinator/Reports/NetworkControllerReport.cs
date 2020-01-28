// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is a network controller report to list out all the network controller status change during test.
    /// </summary>
    class NetworkControllerReport : TestResultReportBase
    {
        public NetworkControllerReport(
            string trackingId,
            string source,
            string resultType,
            IReadOnlyList<TestOperationResult> networkControllerResults)
            : base(trackingId, resultType)
        {
            this.Source = Preconditions.CheckNonWhiteSpace(source, nameof(source));
            this.Results = Preconditions.CheckNotNull(networkControllerResults, nameof(networkControllerResults));
        }

        public string Source { get; }

        public IReadOnlyList<TestOperationResult> Results { get; }

        public override string Title => $"Network Controller Report for [{this.Source}] ({this.ResultType})";

        public override bool IsPassed => true;
    }
}
