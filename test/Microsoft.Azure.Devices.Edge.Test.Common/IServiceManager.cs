// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IServiceManager
    {
        Task StartAsync(CancellationToken token);
        Task StopAsync(CancellationToken token);
        Task ConfigureAsync(CancellationToken token);
        string ConfigurationPath();
        string GetCliName();
    }
}