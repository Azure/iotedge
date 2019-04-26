// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    extern alias akka;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using akka::Akka;
    using akka::Akka.Actor;
    using akka::Akka.IO;
    using Akka.Streams;
    using Akka.Streams.Dsl;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    // Processes incoming logs stream and converts to the required format
    //
    // Docker format -
    // Each input payload should contain one frame in Docker format -
    //    01 00 00 00 00 00 00 1f 52 6f 73 65 73 20 61 72  65 ...
    //    │  ─────┬── ─────┬─────  R o  s e  s a  r e...
    //    │       │        │
    //    └stdout │        │
    //            │        └─ 0x0000001f = 31 bytes (including the \n at the end)
    //         unused
    public class LogsProcessor : ILogsProcessor, IDisposable
    {
        static readonly Flow<ByteString, ByteString, NotUsed> FramingFlow
            = Framing.LengthField(4, int.MaxValue, 4, ByteOrder.BigEndian);

        readonly ActorSystem system;
        readonly ActorMaterializer materializer;
        readonly ILogMessageParser logMessageParser;

        public LogsProcessor(ILogMessageParser logMessageParser)
        {
            this.logMessageParser = Preconditions.CheckNotNull(logMessageParser, nameof(logMessageParser));
            this.system = ActorSystem.Create("LogsProcessor");
            this.materializer = this.system.Materializer();
        }

        public async Task<IReadOnlyList<ModuleLogMessage>> GetMessages(string id, Stream stream, ModuleLogFilter filter)
        {
            Preconditions.CheckNotNull(stream, nameof(stream));
            Preconditions.CheckNotNull(filter, nameof(filter));
            Preconditions.CheckNonWhiteSpace(id, nameof(id));

            GraphBuilder graphBuilder = GraphBuilder.CreateParsingGraphBuilder(stream, b => this.logMessageParser.Parse(b, id));
            filter.LogLevel.ForEach(l => graphBuilder.AddFilter(m => m.LogLevel == l));
            filter.Regex.ForEach(r => graphBuilder.AddFilter(m => r.IsMatch(m.Text)));
            IRunnableGraph<Task<IImmutableList<ModuleLogMessage>>> graph = graphBuilder.GetMaterializingGraph(m => (ModuleLogMessage)m);

            IImmutableList<ModuleLogMessage> result = await graph.Run(this.materializer);
            return result;
        }

        public async Task<IReadOnlyList<string>> GetText(string id, Stream stream, ModuleLogFilter filter)
        {
            Preconditions.CheckNotNull(stream, nameof(stream));
            Preconditions.CheckNotNull(filter, nameof(filter));
            Preconditions.CheckNonWhiteSpace(id, nameof(id));

            IRunnableGraph<Task<IImmutableList<string>>> GetGraph()
            {
                if (filter.Regex.HasValue || filter.LogLevel.HasValue)
                {
                    GraphBuilder graphBuilder = GraphBuilder.CreateParsingGraphBuilder(stream, b => this.logMessageParser.Parse(b, id));
                    filter.LogLevel.ForEach(l => graphBuilder.AddFilter(m => m.LogLevel == l));
                    filter.Regex.ForEach(r => graphBuilder.AddFilter(m => r.IsMatch(m.Text)));
                    return graphBuilder.GetMaterializingGraph(m => m.FullText);
                }
                else
                {
                    return GraphBuilder.BuildMaterializedGraph(stream);
                }
            }

            IRunnableGraph<Task<IImmutableList<string>>> graph = GetGraph();
            IImmutableList<string> result = await graph.Run(this.materializer);
            return result;
        }

        public async Task ProcessLogsStream(string id, Stream stream, ModuleLogOptions logOptions, Func<ArraySegment<byte>, Task> callback)
        {
            GraphBuilder graphBuilder = GraphBuilder.CreateParsingGraphBuilder(stream, b => this.logMessageParser.Parse(b, id));
            logOptions.Filter.LogLevel.ForEach(l => graphBuilder.AddFilter(m => m.LogLevel == l));
            logOptions.Filter.Regex.ForEach(r => graphBuilder.AddFilter(m => r.IsMatch(m.Text)));

            async Task<bool> ConsumerCallback(ArraySegment<byte> a)
            {
                await callback(a);
                return true;
            }

            ArraySegment<byte> BasicMapper(ModuleLogMessageData l)
                => logOptions.ContentType == LogsContentType.Text
                    ? new ArraySegment<byte>(l.FullFrame.ToArray())
                    : new ArraySegment<byte>(l.ToBytes());

            var mappers = new List<Func<ArraySegment<byte>, ArraySegment<byte>>>();
            if (logOptions.ContentEncoding == LogsContentEncoding.Gzip)
            {
                mappers.Add(m => new ArraySegment<byte>(Compression.CompressToGzip(m.Array)));
            }

            IRunnableGraph<Task> graph = graphBuilder.GetStreamingGraph(
                ConsumerCallback,
                BasicMapper,
                mappers);

            await graph.Run(this.materializer);
        }

        public void Dispose()
        {
            this.system?.Dispose();
            this.materializer?.Dispose();
        }

        class GraphBuilder
        {
            Source<ModuleLogMessageData, NotUsed> parsingGraphSource;

            GraphBuilder(Source<ModuleLogMessageData, NotUsed> parsingGraphSource)
            {
                this.parsingGraphSource = parsingGraphSource;
            }

            public static GraphBuilder CreateParsingGraphBuilder(Stream stream, Func<ByteString, ModuleLogMessageData> parserFunc)
            {
                var source = StreamConverters.FromInputStream(() => stream);
                var graph = source
                    .Via(FramingFlow)
                    .Select(parserFunc)
                    .MapMaterializedValue(_ => NotUsed.Instance);
                return new GraphBuilder(graph);
            }

            public static IRunnableGraph<Task<IImmutableList<string>>> BuildMaterializedGraph(Stream stream)
            {
                var source = StreamConverters.FromInputStream(() => stream);
                var seqSink = Sink.Seq<string>();
                IRunnableGraph<Task<IImmutableList<string>>> graph = source
                    .Via(FramingFlow)
                    .Select(b => b.Slice(8))
                    .Select(b => b.ToString(Encoding.UTF8))
                    .ToMaterialized(seqSink, Keep.Right);
                return graph;
            }

            public void AddFilter(Predicate<ModuleLogMessageData> predicate)
            {
                this.parsingGraphSource = this.parsingGraphSource.Where(predicate);
            }

            public IRunnableGraph<Task<IImmutableList<T>>> GetMaterializingGraph<T>(Func<ModuleLogMessageData, T> mapper)
            {
                var seqSink = Sink.Seq<T>();
                return this.parsingGraphSource
                    .Select(mapper)
                    .ToMaterialized(seqSink, Keep.Right);
            }

            public IRunnableGraph<Task> GetStreamingGraph<TU, TV>(Func<TV, Task<TU>> callback, Func<ModuleLogMessageData, TV> basicMapper, IList<Func<TV, TV>> mappers)
            {
                Source<TV, NotUsed> streamingGraphSource = this.parsingGraphSource
                    .Select(basicMapper);

                if (mappers?.Count > 0)
                {
                    foreach (Func<TV, TV> mapper in mappers)
                    {
                        streamingGraphSource = streamingGraphSource.Select(mapper);
                    }
                }

                return streamingGraphSource.SelectAsync(1, callback)
                    .ToMaterialized(Sink.Ignore<TU>(), Keep.Right);
            }
        }
    }
}
