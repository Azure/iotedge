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

        public Task StartAsync(CancellationToken token) => this.manager.StartAsync(token);

        public Task StopAsync(CancellationToken token) => this.manager.StopAsync(token);
    }
}
