// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    extern alias akka;
    using System;
    using akka::Akka.IO;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class ModuleLogMessageData : ModuleLogMessage
    {
        public ModuleLogMessageData(
            string iotHub,
            string deviceId,
            string moduleId,
            string stream,
            int logLevel,
            Option<DateTime> timeStamp,
            string text,
            ByteString fullFrame,
            string fullText)
            : base(iotHub, deviceId, moduleId, stream, logLevel, timeStamp, text)
        {
            this.FullText = Preconditions.CheckNonWhiteSpace(fullText, fullText);
            this.FullFrame = Preconditions.CheckNotNull(fullFrame, nameof(fullFrame));
        }

        [JsonIgnore]
        public ByteString FullFrame { get; }

        [JsonIgnore]
        public string FullText { get; }
    }
}
