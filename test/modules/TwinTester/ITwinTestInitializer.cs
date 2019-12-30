// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    interface ITwinTestInitializer : IDisposable
    {
        Task StartAsync(CancellationToken cancellationToken);
    }
}
