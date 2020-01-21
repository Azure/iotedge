// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    sealed class NetworkControllerReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(DeploymentTestReportGenerator));

        readonly string trackingId;

        internal NetworkControllerReportGenerator(
            string trackingId,
            string source,
            ITestResultCollection<TestOperationResult> testResults)
        {
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.Source = Preconditions.CheckNonWhiteSpace(source, nameof(source));
            this.TestResults = Preconditions.CheckNotNull(testResults, nameof(testResults));
            this.ResultType = TestOperationResultType.Network.ToString();
        }

        internal string Source { get; }

        internal ITestResultCollection<TestOperationResult> TestResults { get; }

        internal string ResultType { get; }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Start to generate report by {nameof(NetworkControllerReportGenerator)} for Source [{this.Source}].");

            var results = new List<TestOperationResult>();

            while (await this.TestResults.MoveNextAsync())
            {
                this.ValidateResult(this.TestResults.Current);
                results.Add(this.TestResults.Current);
            }

            return new NetworkControllerReport(
                this.trackingId,
                this.Source,
                this.ResultType,
                results.AsReadOnly());
        }

        void ValidateResult(TestOperationResult current)
        {
            if (!current.Source.Equals(this.Source, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Result source is '{current.Source}' but expected should be '{this.Source}'.");
            }

            if (!current.Type.Equals(this.ResultType, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Result type is '{current.Type}' but expected should be '{this.ResultType}'.");
            }
        }
    }
}
