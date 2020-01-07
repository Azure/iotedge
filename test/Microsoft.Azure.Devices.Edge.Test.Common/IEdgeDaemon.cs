// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public enum EdgeDaemonStatus
    {
        Running = ServiceControllerStatus.Running,
        Stopped = ServiceControllerStatus.Stopped
    }

    public interface IEdgeDaemon
    {
        Task InstallAsync(Option<string> packagesPath, Option<Uri> proxy, CancellationToken token);

        Task ConfigureAsync(Func<DaemonConfiguration, Task<(string message, object[] properties)>> config, CancellationToken token, bool restart = true);

        Task StartAsync(CancellationToken token);

        Task StopAsync(CancellationToken token);

        Task UninstallAsync(CancellationToken token);

        Task WaitForStatusAsync(EdgeDaemonStatus desired, CancellationToken token);
    }
}
