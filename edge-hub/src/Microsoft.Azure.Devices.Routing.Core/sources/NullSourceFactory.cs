// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Sources
{
    using System.Threading;
    using System.Threading.Tasks;

    public class NullSourceFactory : ISourceFactory
    {
        public static ISourceFactory Instance { get; } = new NullSourceFactory();

        public Task<Source> CreateAsync(string hubName, Router router, CancellationToken token) =>
            Task.FromResult((Source)new NullSource(router));
    }
}
