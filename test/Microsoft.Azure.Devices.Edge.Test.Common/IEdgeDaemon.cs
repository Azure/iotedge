// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IEdgeDaemon
    {
        Task InstallAsync(Option<Uri> proxy, CancellationToken token);

        Task ConfigureAsync(
            Func<DaemonConfiguration, Task<(string message, object[] properties)>> config,
            CancellationToken token,
            bool restart = true);

        Task StartAsync(CancellationToken token);

        Task StopAsync(CancellationToken token);

        Task UninstallAsync(CancellationToken token);

        string GetCertificatesPath();

        IotedgeCli GetCli();
    }
}
