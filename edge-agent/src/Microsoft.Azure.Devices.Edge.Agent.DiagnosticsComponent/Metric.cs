// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class Metric
    {
        public DateTime TimeGeneratedUtc { get; }
        public string Name { get; }
        public double Value { get; }
        public string Tags { get; }

        public Metric(DateTime timeGeneratedUtc, string name, double value, string tags)
        {
            this.TimeGeneratedUtc = timeGeneratedUtc;
            this.Name = name;
            this.Value = value;
            this.Tags = tags;
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
