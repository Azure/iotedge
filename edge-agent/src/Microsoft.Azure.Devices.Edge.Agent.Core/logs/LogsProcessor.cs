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

        public async Task<IReadOnlyList<ModuleLogMessage>> GetMessages(Stream stream, string moduleId)
        {
            Preconditions.CheckNotNull(stream, nameof(stream));
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));

            var source = StreamConverters.FromInputStream(() => stream);
            var seqSink = Sink.Seq<ModuleLogMessage>();
            IRunnableGraph<Task<IImmutableList<ModuleLogMessage>>> graph = source
                .Via(FramingFlow)
                .Select(b => this.logMessageParser.Parse(b, moduleId))
                .ToMaterialized(seqSink, Keep.Right);

            IImmutableList<ModuleLogMessage> result = await graph.Run(this.materializer);
            return result;
        }

        public async Task<IReadOnlyList<string>> GetText(Stream stream)
        {
            Preconditions.CheckNotNull(stream, nameof(stream));
            var source = StreamConverters.FromInputStream(() => stream);
            var seqSink = Sink.Seq<string>();
            IRunnableGraph<Task<IImmutableList<string>>> graph = source
                .Via(FramingFlow)
                .Select(b => b.Slice(8))
                .Select(b => b.ToString(Encoding.UTF8))
                .ToMaterialized(seqSink, Keep.Right);

            IImmutableList<string> result = await graph.Run(this.materializer);
            return result;
        }

        public void Dispose()
        {
            this.system?.Dispose();
            this.materializer?.Dispose();
        }
    }
}
