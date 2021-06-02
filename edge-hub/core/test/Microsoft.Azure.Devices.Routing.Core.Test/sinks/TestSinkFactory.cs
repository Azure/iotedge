// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Sinks
{
    using System.Collections.Immutable;
    using System.Threading.Tasks;

    public class TestSinkFactory<T> : ISinkFactory<T>
    {
        readonly object sync = new object();
        volatile ImmutableDictionary<string, TestSink<T>> sinks = ImmutableDictionary<string, TestSink<T>>.Empty;

        public Task<ISink<T>> CreateAsync(string hubName)
        {
            TestSink<T> sink;
            lock (this.sync)
            {
                if (!this.sinks.TryGetValue(hubName, out sink))
                {
                    sink = new TestSink<T>();
                    this.sinks = this.sinks.Add(hubName, sink);
                }
            }

            return Task.FromResult<ISink<T>>(sink);
        }
    }
}
