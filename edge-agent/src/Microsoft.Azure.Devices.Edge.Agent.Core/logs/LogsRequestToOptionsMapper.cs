// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LogsRequestToOptionsMapper : ILogsRequestToOptionsMapper
    {
        readonly IRuntimeInfoProvider runtimeInfoProvider;
        readonly LogsContentEncoding contentEncoding;
        readonly LogsContentType contentType;
        readonly bool follow;
        readonly LogOutputFraming outputFraming;
        readonly Option<LogsOutputGroupingConfig> outputGroupingConfig;

        public LogsRequestToOptionsMapper(
            IRuntimeInfoProvider runtimeInfoProvider,
            LogsContentEncoding contentEncoding,
            LogsContentType contentType,
            LogOutputFraming outputFraming,
            Option<LogsOutputGroupingConfig> outputGroupingConfig,
            bool follow)
        {
            this.runtimeInfoProvider = Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider));
            this.contentType = contentType;
            this.contentEncoding = contentEncoding;
            this.outputFraming = outputFraming;
            this.outputGroupingConfig = outputGroupingConfig;
            this.follow = follow;
        }

        public async Task<IList<(string id, ModuleLogOptions logOptions)>> MapToLogOptions(IEnumerable<LogRequestItem> requestItems, CancellationToken cancellationToken)
        {
            IList<string> allIds = (await this.runtimeInfoProvider.GetModules(cancellationToken))
                .Select(m => m.Name)
                .ToList();

            IList<(string regex, ModuleLogOptions logOptions)> logOptionsList = requestItems.Select(
                p => (p.Id, new ModuleLogOptions(
                    this.contentEncoding,
                    this.contentType,
                    p.Filter,
                    this.outputFraming,
                    this.outputGroupingConfig,
                    this.follow))).ToList();
            IDictionary<string, ModuleLogOptions> idsToProcess = GetIdsToProcess(logOptionsList, allIds);
            return idsToProcess.Select(kvp => (kvp.Key, kvp.Value)).ToList();
        }

        internal static IDictionary<string, ModuleLogOptions> GetIdsToProcess(IList<(string id, ModuleLogOptions logOptions)> idList, IList<string> allIds)
        {
            var idsToProcess = new Dictionary<string, ModuleLogOptions>(StringComparer.OrdinalIgnoreCase);
            foreach ((string regex, ModuleLogOptions logOptions) in idList)
            {
                ISet<string> ids = GetMatchingIds(regex, allIds);
                if (ids.Count != 0)
                {
                    foreach (string id in ids)
                    {
                        if (!idsToProcess.ContainsKey(id))
                        {
                            idsToProcess[id] = logOptions;
                        }
                    }
                }
            }

            return idsToProcess;
        }

        internal static ISet<string> GetMatchingIds(string id, IEnumerable<string> ids)
        {
            if (!id.Equals(Constants.AllModulesIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex(id, RegexOptions.IgnoreCase);
                ids = ids.Where(m => regex.IsMatch(m));
            }

            return ids.ToImmutableHashSet();
        }
    }
}
