// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.ReporterClients
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public sealed class EventReporterClient : ReporterClientBase
    {
        ILogger logger;
        ModuleClient moduleClient;

        public EventReporterClient(ILogger logger)
            : base(logger)
        {
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        public override void Dispose()
        {
            this.moduleClient?.Dispose();
        }

        internal async Task<EventReporterClient> InitAsync(TransportType transportType)
        {
            this.moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    transportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    this.logger);
            return this;
        }

        internal override async Task ReportStatusAsync(TestResultBase report)
        {
            switch (report.GetType().Name)
            {
                case nameof(LegacyDirectMethodTestResult):
                    TestResultBase shadowReport = report as LegacyDirectMethodTestResult;
                    //BEARWASHERE -- TEST
                    this.logger.LogInformation($"Sending test result - Module: Source={report.Source}, Type={report.ResultType}, CreatedAt={report.CreatedAt}");
                    await this.moduleClient.SendEventAsync("AnyOutput", new Message(Encoding.UTF8.GetBytes($"Source:{shadowReport.Source} CreatedAt:{shadowReport.CreatedAt}.")));
                    break;

                default:
                    throw new NotImplementedException($"{this.GetType().Name} does not have an implementation for {report.GetType().Name} type");
            }
        }
    }
}
