// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    extern alias akka;
    using System;
    using System.Text;
    using System.Text.RegularExpressions;
    using akka::Akka.IO;
    using Microsoft.Azure.Devices.Edge.Util;

    // Parses logs into message objects.
    //
    // Expected format -
    // Each input payload should contain one frame in Docker format -
    //    01 00 00 00 00 00 00 1f 52 6f 73 65 73 20 61 72  65 ...
    //    │  ─────┬── ─────┬─────  R o  s e  s a  r e...
    //    │       │        │
    //    └stdout │        │
    //            │        └─ 0x0000001f = 31 bytes (including the \n at the end)
    //         unused
    //
    // The payload itself is expected to be in this format -
    // <logLevel> TimeStamp log text
    // For example, this log line will be parsed as follows -
    // <6> 2019-02-14 16:15:35.243 -08:00 [INF] [EdgeHub] - Version - 1.0.7-dev.BUILDNUMBER (COMMITID)
    // LogLevel = 6
    // TimeStamp = 2019-02-14 16:15:35.243 -08:00
    // Text = [INF] [EdgeHub] - Version - 1.0.7-dev.BUILDNUMBER (COMMITID)
    public class LogMessageParser : ILogMessageParser
    {
        const int DefaultLogLevel = 6;
        const string LogRegexPattern = @"^(<(?<logLevel>\d)>)?\s*((?<timestamp>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}.\d{3}\s[+-]\d{2}:\d{2})\s)?\s*(?<logtext>.*)";

        readonly string iotHubName;
        readonly string deviceId;

        public LogMessageParser(string iotHubName, string deviceId)
        {
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
        }

        public ModuleLogMessage Parse(ByteString byteString, string moduleId) =>
            GetLogMessage(byteString, this.iotHubName, this.deviceId, moduleId);

        internal static ModuleLogMessage GetLogMessage(ByteString arg, string iotHubName, string deviceId, string moduleId)
        {
            string stream = GetStream(arg[0]);
            ByteString payload = arg.Slice(8);
            string payloadString = payload.ToString(Encoding.UTF8);
            (int logLevel, Option<DateTime> timeStamp, string logText) = ParseLogText(payloadString);
            var moduleLogMessage = new ModuleLogMessage(iotHubName, deviceId, moduleId, stream, logLevel, timeStamp, logText);
            return moduleLogMessage;
        }

        internal static string GetStream(byte streamByte) => streamByte == 2 ? "stderr" : "stdout";

        internal static (int logLevel, Option<DateTime> timeStamp, string text) ParseLogText(string value)
        {
            var regex = new Regex(LogRegexPattern);
            var match = regex.Match(value);
            int logLevel = DefaultLogLevel;
            string text = value;
            Option<DateTime> timeStamp = Option.None<DateTime>();
            if (match.Success)
            {
                var tsg = match.Groups["timestamp"];
                if (tsg?.Length > 0)
                {
                    if (DateTime.TryParse(tsg.Value, out DateTime dt))
                    {
                        timeStamp = Option.Some(dt);
                    }
                }

                var llg = match.Groups["logLevel"];
                if (llg?.Length > 0)
                {
                    string ll = llg.Value;
                    int.TryParse(ll, out logLevel);
                }

                var textGroup = match.Groups["logtext"];
                if (textGroup?.Length > 0)
                {
                    text = textGroup.Value;
                }
            }

            return (logLevel, timeStamp, text);
        }
    }
}
