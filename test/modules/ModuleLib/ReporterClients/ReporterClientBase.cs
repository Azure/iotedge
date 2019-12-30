// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.ReporterClients
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public abstract class ReporterClientBase : IDisposable
    {
        ILogger logger;
        string source;

        protected ReporterClientBase(
            ILogger logger,
            string source)
        {
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.source = Preconditions.CheckNonWhiteSpace(source, nameof(source));
        }
        public abstract void Dispose();

        public async Task ReportStatus(Object report)
        {
            try
            {
                //await this.trcClient.ReportResultAsync(new TestOperationResult { Source = this.source, Result = body, CreatedAt = DateTime.UtcNow, Type = testOperationResultType});
                await this.ReportStatusAsync(report, this.source);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.ToString());
            }
        }
        internal abstract Task ReportStatusAsync(Object report, string source);
    }
}