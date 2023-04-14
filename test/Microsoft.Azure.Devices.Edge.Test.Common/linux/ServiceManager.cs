// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System.Threading;
    using System.Threading.Tasks;

    abstract class ServiceManager
    {
        public abstract Task StartAsync(CancellationToken token);
        public abstract Task StopAsync(CancellationToken token);
    }
}
