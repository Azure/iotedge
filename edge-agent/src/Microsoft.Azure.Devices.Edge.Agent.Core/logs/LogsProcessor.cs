// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    extern alias akka;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using akka::Akka;
    using akka::Akka.Actor;
    using akka::Akka.IO;
    using Akka.Streams;
    using Akka.Streams.Dsl;
    using Akka.Streams.IO;
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

        public async Task<IReadOnlyList<ModuleLogMessage>> GetMessages(Stream stream, string moduleId, Option<int> logLevel, Option<Regex> regex)
        {
            Preconditions.CheckNotNull(stream, nameof(stream));
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));

            GraphBuilder<ModuleLogMessage> graphBuilder = GraphBuilder.CreateParsingGraphBuilder(stream, b => this.logMessageParser.Parse(b, moduleId));
            logLevel.ForEach(l => graphBuilder.AddFilter(m => m.LogLevel == l));
            regex.ForEach(r => graphBuilder.AddFilter(m => r.IsMatch(m.Text)));
            IRunnableGraph<Task<IImmutableList<ModuleLogMessage>>> graph = graphBuilder.GetMaterializingGraph();

            IImmutableList<ModuleLogMessage> result = await graph.Run(this.materializer);
            return result;
        }

        public async Task<IReadOnlyList<string>> GetText(Stream stream)
        {
            Preconditions.CheckNotNull(stream, nameof(stream));
            var source = StreamConverters.FromInputStream(() => stream);
            var seqSink = Sink.Seq<string>();
            IRunnableGraph<Task<IImmutableList<string>>> graph = GraphBuilder.BuildMaterializedGraph(stream);

            IImmutableList<string> result = await graph.Run(this.materializer);
            return result;
        }

        public void Dispose()
        {
            this.system?.Dispose();
            this.materializer?.Dispose();
        }

        //static class GraphBuilder
        //{
        //    public static GraphBuilder<T> CreateParsingGraphBuilder<T>(Stream stream, Func<ByteString, T> parserFunc)
        //    {
        //        var source = StreamConverters.FromInputStream(() => stream);
        //        var graph = source
        //            .Via(FramingFlow)
        //            .Select(parserFunc);
        //        return new GraphBuilder<T>(graph);
        //    }

        //    public static IRunnableGraph<Task<IImmutableList<string>>> BuildMaterializedGraph(Stream stream)
        //    {
        //        var source = StreamConverters.FromInputStream(() => stream);
        //        var seqSink = Sink.Seq<string>();
        //        IRunnableGraph<Task<IImmutableList<string>>> graph = source
        //            .Via(FramingFlow)
        //            .Select(b => b.Slice(8))
        //            .Select(b => b.ToString(Encoding.UTF8))
        //            .ToMaterialized(seqSink, Keep.Right);
        //        return graph;
        //    }
        //}

        //class GraphBuilder<T>
        //{
        //    Source<T, Task<IOResult>> parsingGraphSource;

        //    public GraphBuilder(Source<T, Task<IOResult>> parsingGraphSource)
        //    {
        //        this.parsingGraphSource = parsingGraphSource;
        //    }

        //    public void AddFilter(Predicate<T> predicate)
        //    {
        //        this.parsingGraphSource = this.parsingGraphSource.Where(predicate);
        //    }

        //    public IRunnableGraph<Task<IImmutableList<T>>> GetMaterializingGraph()
        //    {
        //        var seqSink = Sink.Seq<T>();
        //        return this.parsingGraphSource.ToMaterialized(seqSink, Keep.Right);
        //    }
        //}

        static class GraphBuilder
        {
            public static GraphBuilder<T> CreateParsingGraphBuilder<T>(Stream stream, Func<ByteString, T> parserFunc)
            {
                var source = StreamConverters.FromInputStream(() => stream);
                var graph = source
                    .Via(FramingFlow)
                    .Select(parserFunc);
                return new GraphBuilder<T>(graph);
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
        }

        class GraphBuilder<T>
        {
            Source<T, Task<IOResult>> parsingGraphSource;

            public GraphBuilder(Source<T, Task<IOResult>> parsingGraphSource)
            {
                this.parsingGraphSource = parsingGraphSource;
            }

            public void AddFilter(Predicate<T> predicate)
            {
                this.parsingGraphSource = this.parsingGraphSource.Where(predicate);
            }

            public IRunnableGraph<Task<IImmutableList<T>>> GetMaterializingGraph()
            {
                var seqSink = Sink.Seq<T>();
                return this.parsingGraphSource.ToMaterialized(seqSink, Keep.Right);
            }
        }
    }
}
