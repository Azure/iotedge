// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Metric
    {
        public DateTime TimeGeneratedUtc { get; }
        public string Name { get; }
        public double Value { get; }
        public string Tags { get; }

        public Metric(DateTime timeGeneratedUtc, string name, double value, string tags)
        {
            Preconditions.CheckArgument(timeGeneratedUtc.Kind == DateTimeKind.Utc, $"{nameof(timeGeneratedUtc)} was not utc.");
            this.TimeGeneratedUtc = Preconditions.CheckNotNull(timeGeneratedUtc, nameof(timeGeneratedUtc));
            this.Name = Preconditions.CheckNotNull(name, nameof(name));
            this.Value = value;
            this.Tags = Preconditions.CheckNotNull(tags, nameof(tags));
        }

        public int GetValuelessHash()
        {
            // TODO: replace with when upgraded to .net standard 2.1: https://docs.microsoft.com/en-us/dotnet/api/system.hashcode.combine?view=netstandard-2.1
            // return HashCode.Combine(Name.GetHashCode(), Tags.GetHashCode());
            int hash = 17;
            hash = hash * 31 + this.Name.GetHashCode();
            hash = hash * 31 + this.Tags.GetHashCode();

            return hash;
        }
    }
}
