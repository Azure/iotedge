// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    extern alias akka;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Akka.Streams;
    using Akka.Streams.Dsl;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using AkkaActor = akka::Akka.Actor;
    using AkkaIO = akka::Akka.IO;
    using AkkaNet = akka::Akka;

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
        static readonly Flow<AkkaIO.ByteString, AkkaIO.ByteString, AkkaNet.NotUsed> FramingFlow
            = Framing.LengthField(4, int.MaxValue, 4, AkkaIO.ByteOrder.BigEndian);

        static readonly Flow<AkkaIO.ByteString, AkkaIO.ByteString, AkkaNet.NotUsed> SimpleLengthFraming
            = Framing.SimpleFramingProtocolEncoder(int.MaxValue);

        readonly AkkaActor.ActorSystem system;
        readonly ActorMaterializer materializer;
        readonly ILogMessageParser logMessageParser;

        public LogsProcessor(ILogMessageParser logMessageParser)
        {
            this.logMessageParser = Preconditions.CheckNotNull(logMessageParser, nameof(logMessageParser));
            this.system = AkkaActor.ActorSystem.Create("LogsProcessor");
            this.materializer = this.system.Materializer();
        }

        // Gzip encoding or output framing don't apply to this method.
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

        // Gzip encoding or output framing don't apply to this method.
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
                    ? new ArraySegment<byte>(l.FullText.ToBytes())
                    : new ArraySegment<byte>(l.ToBytes());

            var sourceMappers = new List<Func<Source<ArraySegment<byte>, AkkaNet.NotUsed>, Source<ArraySegment<byte>, AkkaNet.NotUsed>>>();

            if (logOptions.ContentEncoding == LogsContentEncoding.Gzip)
            {
                sourceMappers.Add(
                    s => logOptions.OutputGroupingConfig.Map(o => GroupingGzipMapper(s, o))
                        .GetOrElse(() => NonGroupingGzipMapper(s)));
            }

            if (logOptions.OutputFraming == LogOutputFraming.SimpleLength)
            {
                sourceMappers.Add(SimpleLengthFramingMapper);
            }

            IRunnableGraph<Task> graph = graphBuilder.GetStreamingGraph(
                ConsumerCallback,
                BasicMapper,
                sourceMappers);

            await graph.Run(this.materializer);
        }

        public void Dispose()
        {
            this.system?.Dispose();
            this.materializer?.Dispose();
        }

        static Source<ArraySegment<byte>, AkkaNet.NotUsed> SimpleLengthFramingMapper(Source<ArraySegment<byte>, AkkaNet.NotUsed> s) =>
            s.Select(AkkaIO.ByteString.FromBytes)
                .Via(SimpleLengthFraming)
                .Select(b => new ArraySegment<byte>(b.ToArray()));

        static Source<ArraySegment<byte>, AkkaNet.NotUsed> NonGroupingGzipMapper(Source<ArraySegment<byte>, AkkaNet.NotUsed> s) =>
            s.Select(m => new ArraySegment<byte>(Compression.CompressToGzip(m.ToArray())));

        static Source<ArraySegment<byte>, AkkaNet.NotUsed> GroupingGzipMapper(Source<ArraySegment<byte>, AkkaNet.NotUsed> s, LogsOutputGroupingConfig outputGroupingConfig) =>
            s.GroupedWithin(outputGroupingConfig.MaxFrames, outputGroupingConfig.MaxDuration)
                .Select(b => new ArraySegment<byte>(Compression.CompressToGzip(b.SelectMany(a => a).ToArray())));

        class GraphBuilder
        {
            Source<ModuleLogMessageData, AkkaNet.NotUsed> parsingGraphSource;

            GraphBuilder(Source<ModuleLogMessageData, AkkaNet.NotUsed> parsingGraphSource)
            {
                this.parsingGraphSource = parsingGraphSource;
            }

            public static GraphBuilder CreateParsingGraphBuilder(Stream stream, Func<AkkaIO.ByteString, ModuleLogMessageData> parserFunc)
            {
                var source = StreamConverters.FromInputStream(() => stream);
                var graph = source
                    .Via(FramingFlow)
                    .Select(parserFunc)
                    .MapMaterializedValue(_ => AkkaNet.NotUsed.Instance);
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

            public IRunnableGraph<Task> GetStreamingGraph<TU, TV>(
                Func<TV, Task<TU>> callback,
                Func<ModuleLogMessageData, TV> basicMapper,
                List<Func<Source<TV, AkkaNet.NotUsed>, Source<TV, AkkaNet.NotUsed>>> mappers)
            {
                Source<TV, AkkaNet.NotUsed> streamingGraphSource = this.parsingGraphSource
                    .Select(basicMapper);

                if (mappers?.Count > 0)
                {
                    foreach (Func<Source<TV, AkkaNet.NotUsed>, Source<TV, AkkaNet.NotUsed>> mapper in mappers)
                    {
                        streamingGraphSource = mapper(streamingGraphSource);
                    }
                }

                return streamingGraphSource.SelectAsync(1, callback)
                    .ToMaterialized(Sink.Ignore<TU>(), Keep.Right);
            }
        }
    }
}
