// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    extern alias akka;
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class ModuleLogMessage
    {
        public ModuleLogMessage(
            string iotHub,
            string deviceId,
            string moduleId,
            string stream,
            int logLevel,
            Option<DateTime> timeStamp,
            string text)
        {
            this.IoTHub = Preconditions.CheckNonWhiteSpace(iotHub, nameof(iotHub));
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.Text = Preconditions.CheckNotNull(text, nameof(text));
            this.Stream = Preconditions.CheckNotNull(stream, nameof(stream));
            this.LogLevel = logLevel;
            this.TimeStamp = timeStamp;
        }

        [JsonConstructor]
        ModuleLogMessage(string iotHub, string deviceId, string moduleId, string stream, int logLevel, DateTime? timeStamp, string text)
            : this(iotHub, deviceId, moduleId, stream, logLevel, Option.Maybe(timeStamp), text)
        {
        }

        [JsonProperty(PropertyName = "iothub")]
        public string IoTHub { get; }

        [JsonProperty(PropertyName = "device")]
        public string DeviceId { get; }

        [JsonProperty(PropertyName = "id")]
        public string ModuleId { get; }

        [JsonProperty(PropertyName = "stream")]
        public string Stream { get; }

        [JsonProperty(PropertyName = "loglevel")]
        public int LogLevel { get; }

        [JsonProperty(PropertyName = "text")]
        public string Text { get; }

        [JsonProperty(PropertyName = "timestamp")]
        [JsonConverter(typeof(OptionConverter<DateTime>))]
        public Option<DateTime> TimeStamp { get; }
    }
}
