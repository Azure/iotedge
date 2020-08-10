// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
            Stream logsStream = await this.runtimeInfoProvider.GetModuleLogs(id, false, logOptions.Filter.Tail, logOptions.Filter.Since, logOptions.Filter.Until, cancellationToken);
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

        internal async Task GetLogsStreamInternal(string id, ModuleLogOptions logOptions, Func<ArraySegment<byte>, Task> callback, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(logOptions, nameof(logOptions));
            Preconditions.CheckNotNull(callback, nameof(callback));

            Stream logsStream = await this.runtimeInfoProvider.GetModuleLogs(id, logOptions.Follow, logOptions.Filter.Tail, logOptions.Filter.Since, logOptions.Filter.Until, cancellationToken);
            Events.ReceivedStream(id);

            await this.logsProcessor.ProcessLogsStream(id, logsStream, logOptions, callback);
        }

        static byte[] ProcessByContentEncoding(byte[] bytes, LogsContentEncoding contentEncoding) =>
            contentEncoding == LogsContentEncoding.Gzip
                ? Compression.CompressToGzip(bytes)
                : bytes;

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
