// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Sinks
{
    using System.Threading.Tasks;

    public class NullSinkFactory<T> : ISinkFactory<T>
    {
        public Task<ISink<T>> CreateAsync(string hubName) => Task.FromResult<ISink<T>>(new NullSink<T>());
    }
}