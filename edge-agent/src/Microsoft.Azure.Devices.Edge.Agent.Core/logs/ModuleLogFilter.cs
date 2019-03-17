// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Edge.Util;
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

        public Option<int> Tail { get; }

        public Option<int> Since { get; }

        public Option<int> LogLevel { get; }

        public Option<Regex> Regex { get; }
    }
}
