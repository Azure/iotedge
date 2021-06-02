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
        public static ModuleLogFilter Empty = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.None<string>());

        public ModuleLogFilter(Option<int> tail, Option<string> since, Option<string> until, Option<int> logLevel, Option<string> regex)
        {
            this.Tail = tail;
            this.Since = since;
            this.Until = until;
            this.LogLevel = logLevel;
            this.RegexString = regex;
            this.Regex = regex.Map(r => new Regex(r));
        }

        [JsonConstructor]
        ModuleLogFilter(int? tail, string since, string until, int? loglevel, string regex)
            : this(Option.Maybe(tail), Option.Maybe(since), Option.Maybe(until), Option.Maybe(loglevel), Option.Maybe(regex))
        {
        }

        [JsonProperty("tail")]
        [JsonConverter(typeof(OptionConverter<int>), true)]
        public Option<int> Tail { get; }

        [JsonProperty("since")]
        [JsonConverter(typeof(OptionConverter<string>), true)]
        public Option<string> Since { get; }

        [JsonProperty("until")]
        [JsonConverter(typeof(OptionConverter<string>), true)]
        public Option<string> Until { get; }

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
            => other != null &&
               this.Tail.Equals(other.Tail) &&
               this.Since.Equals(other.Since) &&
               this.LogLevel.Equals(other.LogLevel) &&
               this.RegexString.Equals(other.RegexString);

        public override int GetHashCode()
        {
            var hashCode = -132418255;
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<int>>.Default.GetHashCode(this.Tail);
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<string>>.Default.GetHashCode(this.Since);
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<int>>.Default.GetHashCode(this.LogLevel);
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<string>>.Default.GetHashCode(this.RegexString);
            return hashCode;
        }
    }
}
