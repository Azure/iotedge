// Copyright (c) Microsoft. All rights reserved.
namespace CloudToDeviceMessageTester
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    interface ICloudToDeviceMessageTester : IDisposable
    {
        Task StartAsync(CancellationToken ct);
    }
}
