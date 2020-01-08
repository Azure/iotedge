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

    public sealed class ModuleReporterClient : ReporterClientBase
    {
        ILogger logger;
        ModuleClient moduleClient;

        private ModuleReporterClient(
            ModuleClient moduleClient,
            ILogger logger)
            : base(logger)
        {
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.moduleClient = Preconditions.CheckNotNull(moduleClient, nameof(moduleClient));
        }

        public static ModuleReporterClient Create(TransportType transportType, ILogger logger)
            => CreateAsync(transportType, logger).Result;
        public static async Task<ModuleReporterClient> CreateAsync(TransportType transportType, ILogger logger)
        {
            ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    transportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    logger);
            return new ModuleReporterClient(
                moduleClient,
                logger);
        }

        public override void Dispose()
        {
            this.moduleClient?.Dispose();
        }

        internal override async Task ReportStatusAsync(TestResultBase report)
        {
            switch (report.GetType().Name)
            {
                case nameof(LegacyDirectMethodTestResult):
                    TestResultBase shadowReport = report as LegacyDirectMethodTestResult;
                    await this.moduleClient.SendEventAsync("AnyOutput", new Message(Encoding.UTF8.GetBytes($"Source:{shadowReport.Source} CreatedAt:{shadowReport.CreatedAt}.")));
                    break;

                default:
                    throw (new NotImplementedException($"{this.GetType().Name} does not have an implementation for {report.GetType().Name} type"));
            }
        }
    }
}
