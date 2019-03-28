// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class ModuleLogFilter : IEquatable<ModuleLogFilter>
    {
        public ModuleLogFilter(Option<int> tail, Option<int> since, Option<int> logLevel, Option<string> regex)
        {
            this.Tail = tail;
            this.Since = since;
            this.LogLevel = logLevel;
            this.RegexString = regex;
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

        [JsonIgnore]
        public Option<Regex> Regex { get; }

        [JsonProperty("regex")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> RegexString { get; }

        public override bool Equals(object obj)
            => this.Equals(obj as ModuleLogFilter);

        public bool Equals(ModuleLogFilter other)
        {
            return other != null &&
                   this.Tail.Equals(other.Tail) &&
                   this.Since.Equals(other.Since) &&
                   this.LogLevel.Equals(other.LogLevel) &&
                   this.Regex.Equals(other.Regex);
        }

        public override int GetHashCode()
        {
            var hashCode = -132418255;
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<int>>.Default.GetHashCode(this.Tail);
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<int>>.Default.GetHashCode(this.Since);
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<int>>.Default.GetHashCode(this.LogLevel);
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<Regex>>.Default.GetHashCode(this.Regex);
            return hashCode;
        }
    }
}
