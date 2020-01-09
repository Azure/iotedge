// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    internal sealed class EventReporterClient : ReporterClientBase
    {
        ILogger logger;
        TransportType transportType;
        ModuleClient moduleClient = null;
        ModuleClient ModuleClient
        {
            get
            {
                if (this.moduleClient == null)
                {
                    Preconditions.CheckNotNull(this.transportType, nameof(this.transportType));
                    this.moduleClient = ModuleUtil.CreateModuleClientAsync(
                        this.transportType,
                        ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                        ModuleUtil.DefaultTransientRetryStrategy,
                        this.logger).Result;
                    return this.moduleClient;
                }
                else
                {
                    return this.moduleClient;
                }
            }
        }

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
            this.transportType = Preconditions.CheckNotNull(transportType, nameof(transportType));
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
                    LegacyDirectMethodTestResult shadowReport = report as LegacyDirectMethodTestResult;
                    // The Old End-to-End test framework checks if there is an event in the eventHub for a particular module
                    // to determine if the test case is passed/failed. Hence, this function need to check if the report status is Http.OK
                    // before sending an event.
                    if (shadowReport.Result == HttpStatusCode.OK.ToString())
                    {
                        await this.ModuleClient.SendEventAsync("AnyOutput", new Message(Encoding.UTF8.GetBytes($"Source:{shadowReport.Source} CreatedAt:{shadowReport.CreatedAt}.")));
                    }

                    break;

                default:
                    throw new NotImplementedException($"{this.GetType().Name} does not have an implementation for {report.GetType().Name} type");
            }
        }
    }
}
