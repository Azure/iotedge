// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Collections.Generic;

    public class LogsOutputGroupingConfig : IEquatable<LogsOutputGroupingConfig>
    {
        public LogsOutputGroupingConfig(int maxFrames, TimeSpan maxDuration)
        {
            this.MaxFrames = maxFrames;
            this.MaxDuration = maxDuration;
        }

        public int MaxFrames { get; }

        public TimeSpan MaxDuration { get; }

        public override bool Equals(object obj)
            => this.Equals(obj as LogsOutputGroupingConfig);

        public bool Equals(LogsOutputGroupingConfig other)
            => other != null &&
               this.MaxFrames == other.MaxFrames &&
               this.MaxDuration.Equals(other.MaxDuration);

        public override int GetHashCode()
        {
            var hashCode = -1599005300;
            hashCode = hashCode * -1521134295 + this.MaxFrames.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<TimeSpan>.Default.GetHashCode(this.MaxDuration);
            return hashCode;
        }
    }
}
