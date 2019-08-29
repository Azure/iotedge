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
        // install with manual provisioning
        Task InstallAsync(string deviceConnectionString, Option<string> packagesPath, Option<Uri> proxy, CancellationToken token);

        // install with DPS symmetric key attestion
        Task InstallAsync(string scopeId, string registrationId, string symmetricKey, Option<string> packagesPath, Option<Uri> proxy, CancellationToken token);

        Task ConfigureAsync(Func<DaemonConfiguration, Task<(string message, object[] properties)>> config, CancellationToken token);

        Task StartAsync(CancellationToken token);

        Task StopAsync(CancellationToken token);

        Task UninstallAsync(CancellationToken token);

        Task WaitForStatusAsync(EdgeDaemonStatus desired, CancellationToken token);
    }
}
