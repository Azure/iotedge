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

        public async Task<byte[]> GetLogs(ModuleLogOptions logOptions, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(logOptions, nameof(logOptions));
            Stream logsStream = await this.runtimeInfoProvider.GetModuleLogs(logOptions.Id, false, logOptions.Filter.Tail, logOptions.Filter.Since, cancellationToken);
            Events.ReceivedStream(logOptions.Id);

            byte[] logBytes = await this.GetProcessedLogs(logsStream, logOptions);
            return logBytes;
        }

        public async Task GetLogsStream(ModuleLogOptions logOptions, Func<ArraySegment<byte>, Task> callback, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(logOptions, nameof(logOptions));
            Preconditions.CheckNotNull(callback, nameof(callback));

            ISet<string> ids = await this.GetIds(logOptions.Id, cancellationToken);
            IEnumerable<Task> streamingTasks = ids.Select(id => this.GetLogsStream(id, logOptions, callback, cancellationToken));
            await Task.WhenAll(streamingTasks);
        }

        public async Task GetLogsStream(IList<ModuleLogOptions> logOptionsList, Func<ArraySegment<byte>, Task> callback, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(logOptionsList, nameof(logOptionsList));
            Preconditions.CheckNotNull(callback, nameof(callback));

            var idsToProcess = new Dictionary<string, ModuleLogOptions>(StringComparer.OrdinalIgnoreCase);
            foreach (ModuleLogOptions logOptions in logOptionsList)
            {
                ISet<string> ids = await this.GetIds(logOptions.Id, cancellationToken);
                foreach (string id in ids)
                {
                    if (!idsToProcess.ContainsKey(id))
                    {
                        idsToProcess[id] = logOptions;
                    }
                }
            }

            IEnumerable<Task> streamingTasks = idsToProcess.Select(kvp => this.GetLogsStream(kvp.Key, kvp.Value, callback, cancellationToken));
            await Task.WhenAll(streamingTasks);
        }

        internal async Task GetLogsStream(string id, ModuleLogOptions logOptions, Func<ArraySegment<byte>, Task> callback, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(logOptions, nameof(logOptions));
            Preconditions.CheckNotNull(callback, nameof(callback));

            Stream logsStream = await this.runtimeInfoProvider.GetModuleLogs(id, true, logOptions.Filter.Tail, logOptions.Filter.Since, cancellationToken);
            Events.ReceivedStream(id);

            await (NeedToProcessStream(logOptions)
                ? this.logsProcessor.ProcessLogsStream(logsStream, logOptions, callback)
                : this.WriteLogsStreamToOutput(logOptions.Id, callback, logsStream, cancellationToken));
        }

        internal async Task<ISet<string>> GetIds(string id, CancellationToken cancellationToken)
        {
            IEnumerable<string> modules = (await this.runtimeInfoProvider.GetModules(cancellationToken))
                .Select(m => m.Name);

            if (!id.Equals(Constants.AllModulesIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex("id");
                modules = modules.Where(m => regex.IsMatch(m));
            }

            return modules.ToImmutableHashSet();            
        }

        internal static bool NeedToProcessStream(ModuleLogOptions logOptions) =>
            logOptions.Filter.LogLevel.HasValue
            || logOptions.Filter.Regex.HasValue
            || logOptions.ContentEncoding != LogsContentEncoding.None
            || logOptions.ContentType != LogsContentType.Text;

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

        async Task<byte[]> GetProcessedLogs(Stream logsStream, ModuleLogOptions logOptions)
        {
            byte[] logBytes = await this.ProcessByContentType(logsStream, logOptions);
            logBytes = ProcessByContentEncoding(logBytes, logOptions.ContentEncoding);
            return logBytes;
        }

        async Task<byte[]> ProcessByContentType(Stream logsStream, ModuleLogOptions logOptions)
        {
            switch (logOptions.ContentType)
            {
                case LogsContentType.Json:
                    IEnumerable<ModuleLogMessage> logMessages = await this.logsProcessor.GetMessages(logsStream, logOptions.Id, logOptions.Filter);
                    return logMessages.ToBytes();

                default:
                    IEnumerable<string> logTexts = await this.logsProcessor.GetText(logsStream, logOptions.Id, logOptions.Filter);
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
                StreamingCompleted
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
        }
    }
}
