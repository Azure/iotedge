// Copyright (c) Microsoft. All rights reserved.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Edge.Util;

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System.ServiceProcess;

    public enum EdgeDaemonStatus
    {
        Running = ServiceControllerStatus.Running,
        Stopped = ServiceControllerStatus.Stopped
    }

    public interface IEdgeDaemon
    {
        Task InstallAsync(string deviceConnectionString, Option<string> packagesPath, Option<Uri> proxy, CancellationToken token);

        Task StartAsync(CancellationToken token);

        Task StopAsync(CancellationToken token);

        Task UninstallAsync(CancellationToken token);

        Task WaitForStatusAsync(EdgeDaemonStatus desired, CancellationToken token);
    }
}
