// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class ModuleLogFilter
    {
        public ModuleLogFilter(Option<int> tail, Option<int> since, Option<int> logLevel, Option<string> regex)
        {
            this.Tail = tail;
            this.Since = since;
            this.LogLevel = logLevel;
            this.Regex = regex;
        }

        [JsonConstructor]
        ModuleLogFilter(int? tail, int? since, int? logLevel, string regex)
            : this(Option.Maybe(tail), Option.Maybe(since), Option.Maybe(logLevel), Option.Maybe(regex))
        {
        }

        public Option<int> Tail { get; }
        public Option<int> Since { get; }
        public Option<int> LogLevel { get; }
        public Option<string> Regex { get; }
    }
}
