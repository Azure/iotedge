// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class LogsProvider : ILogsProvider
    {
        readonly IRuntimeInfoProvider runtimeInfoProvider;
        readonly ILogsProcessor logsProcessor;

        public LogsProvider(IRuntimeInfoProvider runtimeInfoProvider, ILogsProcessor logsProcessor)
        {
            this.runtimeInfoProvider = Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider));
            this.logsProcessor = Preconditions.CheckNotNull(logsProcessor, nameof(logsProcessor));
        }

        public async Task<byte[]> GetLogs(string id, ModuleLogOptions logOptions, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(logOptions, nameof(logOptions));
            Stream logsStream = await this.runtimeInfoProvider.GetModuleLogs(id, false, logOptions.Filter.Tail, logOptions.Filter.Since, cancellationToken);
            Events.ReceivedStream(id);

            byte[] logBytes = await this.GetProcessedLogs(id, logsStream, logOptions);
            return logBytes;
        }

        // The id parameter is a regex. Logs for all modules that match this regex are processed.
        public Task GetLogsStream(string id, ModuleLogOptions logOptions, Func<ArraySegment<byte>, Task> callback, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(logOptions, nameof(logOptions));
            Preconditions.CheckNotNull(callback, nameof(callback));

            Events.StreamingLogs(id, logOptions);
            return this.GetLogsStreamInternal(id, logOptions, callback, cancellationToken);
        }

        // The id parameter in the ids is a regex. Logs for all modules that match this regex are processed.
        // If multiple id parameters match a module, the first one is considered.
        public Task GetLogsStream(IList<(string id, ModuleLogOptions logOptions)> ids, Func<ArraySegment<byte>, Task> callback, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(ids, nameof(ids));
            Preconditions.CheckNotNull(callback, nameof(callback));

            Events.StreamingLogs(ids);
            IEnumerable<Task> streamingTasks = ids.Select(item => this.GetLogsStreamInternal(item.id, item.logOptions, callback, cancellationToken));
            return Task.WhenAll(streamingTasks);
        }

        internal static IDictionary<string, ModuleLogOptions> GetIdsToProcess(IList<(string id, ModuleLogOptions logOptions)> idList, IList<string> allIds)
        {
            var idsToProcess = new Dictionary<string, ModuleLogOptions>(StringComparer.OrdinalIgnoreCase);
            foreach ((string regex, ModuleLogOptions logOptions) in idList)
            {
                ISet<string> ids = GetMatchingIds(regex, allIds);
                if (ids.Count == 0)
                {
                    Events.NoMatchingModule(regex, allIds);
                }
                else
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

        internal static bool NeedToProcessStream(ModuleLogOptions logOptions) =>
            logOptions.Filter.LogLevel.HasValue
            || logOptions.Filter.Regex.HasValue
            || logOptions.ContentEncoding != LogsContentEncoding.None
            || logOptions.ContentType != LogsContentType.Text;

        internal async Task GetLogsStreamInternal(string id, ModuleLogOptions logOptions, Func<ArraySegment<byte>, Task> callback, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(logOptions, nameof(logOptions));
            Preconditions.CheckNotNull(callback, nameof(callback));

            Stream logsStream = await this.runtimeInfoProvider.GetModuleLogs(id, true, logOptions.Filter.Tail, logOptions.Filter.Since, cancellationToken);
            Events.ReceivedStream(id);

            await (NeedToProcessStream(logOptions)
                ? this.logsProcessor.ProcessLogsStream(id, logsStream, logOptions, callback)
                : this.WriteLogsStreamToOutput(id, callback, logsStream, cancellationToken));
        }

        static byte[] ProcessByContentEncoding(byte[] bytes, LogsContentEncoding contentEncoding) =>
            contentEncoding == LogsContentEncoding.Gzip
                ? Compression.CompressToGzip(bytes)
                : bytes;

        async Task WriteLogsStreamToOutput(string id, Func<ArraySegment<byte>, Task> callback, Stream stream, CancellationToken cancellationToken)
        {
            var buf = new byte[1024];
            try
            {
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Events.StreamingCancelled(id);
                        break;
                    }

                    int count = await stream.ReadAsync(buf, 0, buf.Length, cancellationToken);
                    if (count == 0)
                    {
                        Events.StreamingCompleted(id);
                        break;
                    }

                    var arrSeg = new ArraySegment<byte>(buf, 0, count);
                    await callback(arrSeg);
                }
            }
            catch (Exception ex)
            {
                Events.ErrorWhileProcessingStream(id, ex);
            }
        }

        async Task<byte[]> GetProcessedLogs(string id, Stream logsStream, ModuleLogOptions logOptions)
        {
            byte[] logBytes = await this.ProcessByContentType(id, logsStream, logOptions);
            logBytes = ProcessByContentEncoding(logBytes, logOptions.ContentEncoding);
            return logBytes;
        }

        async Task<byte[]> ProcessByContentType(string id, Stream logsStream, ModuleLogOptions logOptions)
        {
            switch (logOptions.ContentType)
            {
                case LogsContentType.Json:
                    IEnumerable<ModuleLogMessage> logMessages = await this.logsProcessor.GetMessages(id, logsStream, logOptions.Filter);
                    return logMessages.ToBytes();

                default:
                    IEnumerable<string> logTexts = await this.logsProcessor.GetText(id, logsStream, logOptions.Filter);
                    string logTextString = logTexts.Join(string.Empty);
                    return logTextString.ToBytes();
            }
        }

        static class Events
        {
            const int IdStart = AgentEventIds.LogsProvider;
            static readonly ILogger Log = Logger.Factory.CreateLogger<LogsProvider>();

            enum EventIds
            {
                StreamingCancelled = IdStart,
                ErrorWhileStreaming,
                ReceivedStream,
                StreamingCompleted,
                StreamingLogs,
                NoMatchingModule
            }

            public static void ErrorWhileProcessingStream(string id, Exception ex)
            {
                Log.LogInformation((int)EventIds.ErrorWhileStreaming, $"Error streaming logs for {id}, terminating streaming operation.");
                Log.LogDebug((int)EventIds.ErrorWhileStreaming, ex, $"Streaming error details for {id}");
            }

            public static void StreamingCancelled(string id)
            {
                Log.LogInformation((int)EventIds.StreamingCancelled, $"Streaming logs for {id} cancelled.");
            }

            public static void ReceivedStream(string id)
            {
                Log.LogInformation((int)EventIds.ReceivedStream, $"Initiating streaming logs for {id}");
            }

            public static void StreamingCompleted(string id)
            {
                Log.LogInformation((int)EventIds.StreamingCompleted, $"Completed streaming logs for {id}");
            }

            public static void StreamingLogs(IList<(string id, ModuleLogOptions logOptions)> ids)
            {
                Log.LogDebug((int)EventIds.StreamingLogs, $"Streaming logs for {ids.ToJson()}");
            }

            public static void NoMatchingModule(string regex, IList<string> allIds)
            {
                string idsString = allIds.Join(", ");
                Log.LogWarning((int)EventIds.NoMatchingModule, $"The regex {regex} in the log stream request did not match any of the modules - {idsString}");
            }

            internal static void StreamingLogs(string id, ModuleLogOptions logOptions)
            {
                Log.LogDebug((int)EventIds.StreamingLogs, $"Streaming logs for {id} with options {logOptions.ToJson()}");
            }
        }
    }
}
