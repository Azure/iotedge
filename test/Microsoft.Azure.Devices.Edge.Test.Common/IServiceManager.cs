// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IServiceManager
    {
        Task StartAsync(CancellationToken token);
        Task StopAsync(CancellationToken token);
        Task<string> ReadConfigurationAsync(Service service, CancellationToken token);
        Task WriteConfigurationAsync(Service service, string config, CancellationToken token);
        string GetPrincipalsPath(Service service);
    }
}