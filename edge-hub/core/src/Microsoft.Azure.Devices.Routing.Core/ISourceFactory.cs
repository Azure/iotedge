// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISourceFactory
    {
        Task<Source> CreateAsync(string hubName, Router router, CancellationToken token);
    }
}
