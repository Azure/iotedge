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
        readonly string[] names;
        ServiceManager manager;

        public Services(string[] names, ServiceManagerType manager = ServiceManagerType.Systemd)
        {
            this.names = names;
            this.manager = manager switch
            {
                ServiceManagerType.Systemd => new SystemdServiceManager(names),
                _ => throw new NotImplementedException($"Unknown service manager '{manager.ToString()}'"),
            };
        }

        public Task StartAsync(CancellationToken token) => this.manager.StartAsync(token);

        public Task StopAsync(CancellationToken token) => this.manager.StopAsync(token);
    }
}
