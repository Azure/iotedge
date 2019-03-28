// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LogsUploadRequestHandler : RequestHandlerBase<LogsUploadRequest, object>
    {
        readonly ILogsUploader logsUploader;
        readonly ILogsProvider logsProvider;
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public LogsUploadRequestHandler(ILogsUploader logsUploader, ILogsProvider logsProvider, IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.logsProvider = Preconditions.CheckNotNull(logsProvider, nameof(logsProvider));
            this.logsUploader = Preconditions.CheckNotNull(logsUploader, nameof(logsUploader));
            this.runtimeInfoProvider = Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider));
        }

        public override string RequestName => "UploadLogs";

        protected override async Task<Option<object>> HandleRequestInternal(Option<LogsUploadRequest> payloadOption, CancellationToken cancellationToken)
        {
            LogsUploadRequest payload = payloadOption.Expect(() => new ArgumentException("Request payload not found"));

            async Task<IList<string>> GetModuleIds()
            {
                if (payload.Id == Constants.AllModulesIdentifier)
                {
                    IEnumerable<ModuleRuntimeInfo> modules = await this.runtimeInfoProvider.GetModules(cancellationToken);
                    return modules.Select(m => m.Name).ToList();
                }
                else
                {
                    return new List<string> { payload.Id };
                }
            }

            IList<string> moduleIds = await GetModuleIds();
            IEnumerable<Task> uploadTasks = moduleIds.Select(m => this.UploadLogs(payload.SasUrl, new ModuleLogOptions(m, payload.Encoding, payload.ContentType, payload.Filter), cancellationToken));
            await Task.WhenAll(uploadTasks);
            return Option.None<object>();
        }

        async Task UploadLogs(string sasUrl, ModuleLogOptions moduleLogOptions, CancellationToken token)
        {
            byte[] logBytes = await this.logsProvider.GetLogs(moduleLogOptions, token);
            await this.logsUploader.Upload(sasUrl, moduleLogOptions.Id, logBytes, moduleLogOptions.ContentEncoding, moduleLogOptions.ContentType);
        }
    }
}
