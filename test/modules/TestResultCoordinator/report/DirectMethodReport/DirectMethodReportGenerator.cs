// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report.DirectMethodReport
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.Report;

    sealed class DirectMethodReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(DirectMethodReportGenerator));
        readonly string trackingId;
        readonly string source;
        readonly ISequentialStore<TestOperationResult> store;
        readonly string resultType;
        readonly int batchSize;

        public DirectMethodReportGenerator(
            string trackingId,
            string source,
            ISequentialStore<TestOperationResult> store,
            string resultType,
            int batchSize = 500)
        {
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.source = Preconditions.CheckNonWhiteSpace(source, nameof(source));
            this.store = Preconditions.CheckNotNull(store, nameof(store));
            this.resultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.batchSize = batchSize;
        }

        public Task<ITestResultReport> CreateReportAsync()
        {
            throw new System.NotImplementedException();
        }
    }
}
