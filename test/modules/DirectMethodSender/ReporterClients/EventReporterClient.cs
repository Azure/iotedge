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

    sealed class EventReporterClient : ReporterClientBase
    {
        readonly ILogger logger;
        readonly TransportType transportType;
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
                        new ClientOptions(),
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

        internal EventReporterClient(ILogger logger, TransportType transportType)
            : base(logger)
        {
            this.transportType = Preconditions.CheckNotNull(transportType, nameof(transportType));
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        public override void Dispose()
        {
            this.moduleClient?.Dispose();
        }

        internal override async Task ReportStatusAsync(TestResultBase testResult)
        {
            switch (testResult.GetType().Name)
            {
                case nameof(LegacyDirectMethodTestResult):
                    LegacyDirectMethodTestResult shadowReport = testResult as LegacyDirectMethodTestResult;
                    // The Old End-to-End test framework checks if there is an event in the eventHub for a particular module
                    // to determine if the test case is passed/failed. Hence, this function need to check if the testResult status is Http.OK
                    // before sending an event.
                    if (shadowReport.Result == HttpStatusCode.OK.ToString())
                    {
                        await this.ModuleClient.SendEventAsync("AnyOutput", new Message(Encoding.UTF8.GetBytes($"Source:{shadowReport.Source} CreatedAt:{shadowReport.CreatedAt}.")));
                    }

                    break;

                default:
                    throw new NotSupportedException($"{this.GetType().Name} does not have an implementation for {testResult.GetType().Name} type");
            }
        }
    }
}
