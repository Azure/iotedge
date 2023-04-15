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

    enum ServiceStatus
    {
        Running,
        Stopped
    }

    public class Services
    {
        public IServiceManager Manager { get; }

        public Services(ServiceManagerType managerType = ServiceManagerType.Systemd)
        {
            this.Manager = managerType switch
            {
                ServiceManagerType.Systemd => new SystemdServiceManager(),
                ServiceManagerType.Snap => new SnapServiceManager(),
                _ => throw new NotImplementedException($"Unknown service manager '{managerType.ToString()}'"),
            };
        }

        public Task StartAsync(CancellationToken token) =>
            Profiler.Run(() => this.Manager.StartAsync(token), "Edge daemon entered the running state");

        public Task StopAsync(CancellationToken token) =>
            Profiler.Run(() => this.Manager.StopAsync(token), "Edge daemon entered the stopped state");

        public Task<string> ReadConfigurationAsync(Service service, CancellationToken token) =>
            this.Manager.ReadConfigurationAsync(service, token);

        public Task WriteConfigurationAsync(Service service, string config, CancellationToken token) =>
            this.Manager.WriteConfigurationAsync(service, config, token);
    }
}
