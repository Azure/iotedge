// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    extern alias akka;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using akka::Akka.IO;
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

            ILogsRequestToOptionsMapper requestToOptionsMapper = new LogsRequestToOptionsMapper(
                this.runtimeInfoProvider,
                payload.Encoding,
                payload.ContentType,
                LogOutputFraming.None,
                Option.Some(new LogsOutputGroupingConfig(100, TimeSpan.FromSeconds(10))));
            IList<(string id, ModuleLogOptions logOptions)> logOptionsList = await requestToOptionsMapper.MapToLogOptions(payload.Items, cancellationToken);
            IEnumerable<Task> uploadLogsTasks = logOptionsList.Select(l => this.UploadLogs(payload.SasUrl, l.id, l.logOptions, cancellationToken));
            await Task.WhenAll(uploadLogsTasks);
            return Option.None<object>();
        }

        async Task UploadLogs(string sasUrl, string id, ModuleLogOptions moduleLogOptions, CancellationToken token)
        {
            Console.WriteLine($"Uploading logs for {id}");
            await this.GetAndPrintLogFrames(id);
            if (moduleLogOptions.ContentType == LogsContentType.Json)
            {
                byte[] logBytes = await this.logsProvider.GetLogs(id, moduleLogOptions, token);
                await this.logsUploader.Upload(sasUrl, id, logBytes, moduleLogOptions.ContentEncoding, moduleLogOptions.ContentType);
            }
            else if (moduleLogOptions.ContentType == LogsContentType.Text)
            {
                Func<ArraySegment<byte>, Task> uploaderCallback = await this.logsUploader.GetUploaderCallback(sasUrl, id, moduleLogOptions.ContentEncoding, moduleLogOptions.ContentType);
                await this.logsProvider.GetLogsStream(id, moduleLogOptions, uploaderCallback, token);
            }
        }

        async Task GetAndPrintLogFrames(string id)
        {
            Console.WriteLine($"Getting logs for {id}");
            Stream s = await this.runtimeInfoProvider.GetModuleLogs(id, true, Option.None<int>(), Option.None<int>(), CancellationToken.None);
            Console.WriteLine($"Got logs for {id}.. printing log frames");
            byte[] streamBytes = ReadStream(s);
            PrintLogFrames(streamBytes);
        }

        static void PrintLogFrames(byte[] streamBytes)
        {
            int cnt = 0;
            var bs = ByteString.FromBytes(streamBytes);
            while (bs.Count > 0)
            {
                if (bs.Count < 4)
                {
                    Console.WriteLine("Less than 4 bytes remaining!");
                    PrintBytes(bs.ToArray());
                    break;
                }
                bs = bs.Slice(4);
                if (bs.Count < 4)
                {
                    Console.WriteLine("Less than 8 bytes remaining!");
                    PrintBytes(bs.ToArray());
                    break;
                }
                int lenBytes = GetLen(bs.Slice(0, 4).ToArray());
                if (bs.Count < 4 + lenBytes)
                {
                    Console.WriteLine($"Less than {4 + lenBytes} bytes remaining!");
                    PrintBytes(bs.ToArray());
                    break;
                }
                string str = bs.Slice(4, lenBytes).ToString(Encoding.UTF8);
                Console.WriteLine($"Read frame {cnt} of length {lenBytes} - {str}");
                bs = bs.Slice(4 + lenBytes);
                cnt++;
            }
            Console.WriteLine($"Done printing log frames");
        }

        static void PrintBytes(byte[] bytes)
        {
            var sb = new StringBuilder("Printing byte array - {");
            foreach (var b in bytes)
            {
                sb.Append(b + ", ");
            }
            sb.Append("}");
            Console.WriteLine(sb.ToString());
        }

        static int GetLen(byte[] bytes)
        {
            Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        static byte[] ReadStream(Stream s)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = s.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}
