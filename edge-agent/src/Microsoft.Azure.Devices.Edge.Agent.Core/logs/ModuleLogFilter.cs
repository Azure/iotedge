// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class ModuleLogFilter
    {
        public ModuleLogFilter(Option<int> tail, Option<int> since, Option<int> logLevel, Option<string> regex)
        {
            this.Tail = tail;
            this.Since = since;
            this.LogLevel = logLevel;
            this.Regex = regex.Map(r => new Regex(r));
        }

        [JsonConstructor]
        ModuleLogFilter(int? tail, int? since, int? loglevel, string regex)
            : this(Option.Maybe(tail), Option.Maybe(since), Option.Maybe(loglevel), Option.Maybe(regex))
        {
        }

        public static ModuleLogFilter Empty = new ModuleLogFilter(Option.None<int>(), Option.None<int>(), Option.None<int>(), Option.None<string>());

        [JsonProperty("tail")]
        [JsonConverter(typeof(OptionConverter<int>), true)]
        public Option<int> Tail { get; }

        [JsonProperty("since")]
        [JsonConverter(typeof(OptionConverter<int>), true)]
        public Option<int> Since { get; }

        [JsonProperty("loglevel")]
        [JsonConverter(typeof(OptionConverter<int>), true)]
        public Option<int> LogLevel { get; }

        [JsonProperty("regex")]
        [JsonConverter(typeof(OptionConverter<Regex>))]
        public Option<Regex> Regex { get; }
    }
}
