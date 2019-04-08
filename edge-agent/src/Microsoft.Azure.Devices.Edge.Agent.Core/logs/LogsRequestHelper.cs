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

    public static class LogsRequestHelper
    {
        public static async Task<IList<(string id, ModuleLogOptions logOptions)>> MapToLogOptions(this IEnumerable<LogRequestItem> requestItems, IRuntimeInfoProvider runtimeInfoProvider, LogsContentEncoding encoding, LogsContentType contentType, CancellationToken cancellationToken)
        {
            IList<string> allIds = (await runtimeInfoProvider.GetModules(cancellationToken))
                .Select(m => m.Name)
                .ToList();
            return MapToLogOptions(requestItems, allIds, encoding, contentType);
        }

        internal static IList<(string id, ModuleLogOptions logOptions)> MapToLogOptions(IEnumerable<LogRequestItem> requestItems, IList<string> allIds, LogsContentEncoding encoding, LogsContentType contentType)
        {
            IList<(string regex, ModuleLogOptions logOptions)> logOptionsList = requestItems.Select(p => (p.Id, new ModuleLogOptions(encoding, contentType, p.Filter))).ToList();
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
