// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Sinks
{
    using System.Threading.Tasks;

    public class NullSinkFactory<T> : ISinkFactory<T>
    {
        public Task<ISink<T>> CreateAsync(string hubName) => Task.FromResult<ISink<T>>(new NullSink<T>());
    }
}
