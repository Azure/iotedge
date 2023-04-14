// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public enum ServiceManagerType
    {
        Systemd,
        Snap
    }

    enum ServicesStatus
    {
        Running,
        Stopped
    }

    public class Services
    {
        ServiceManager manager;

        public Services(ServiceManagerType manager = ServiceManagerType.Systemd)
        {
            this.manager = manager switch
            {
                ServiceManagerType.Systemd => new SystemdServiceManager(),
                _ => throw new NotImplementedException($"Unknown service manager '{manager.ToString()}'"),
            };
        }

        public Task StartAsync(CancellationToken token) =>
            Profiler.Run(() => this.manager.StartAsync(token), "Edge daemon entered the running state");

        public Task StopAsync(CancellationToken token) =>
            Profiler.Run(() => this.manager.StopAsync(token), "Edge daemon entered the stopped state");
    }
}
