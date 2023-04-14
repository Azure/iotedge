// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.Linq;
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

        abstract class ServiceManager
        {
            public abstract Task StartAsync(CancellationToken token);
            public abstract Task StopAsync(CancellationToken token);
        }

        class SystemdServiceManager : ServiceManager
        {
            readonly string[] names;

            public SystemdServiceManager(string[] names)
            {
                this.names = names;
            }

            public override async Task StartAsync(CancellationToken token)
            {
                await Process.RunAsync("systemctl", $"start {string.Join(' ', this.names)}", token);
                await Profiler.Run(
                    () => this.WaitForStatusAsync(ServicesStatus.Running, token),
                    "Edge daemon entered the running state");
            }

            public override async Task StopAsync(CancellationToken token)
            {
                await Process.RunAsync("systemctl", $"stop {string.Join(' ', this.names)}", token);
                await Profiler.Run(
                    () => this.WaitForStatusAsync(ServicesStatus.Stopped, token),
                    "Edge daemon entered the stopped state");
            }

            async Task WaitForStatusAsync(ServicesStatus desired, CancellationToken token)
            {
                foreach (string service in this.names)
                {
                    while (true)
                    {
                        Func<string, bool> stateMatchesDesired = desired switch
                        {
                            ServicesStatus.Running => s => s == "active",
                            ServicesStatus.Stopped => s => s == "inactive" || s == "failed",
                            _ => throw new NotImplementedException($"No handler for {desired}"),
                        };

                        string[] output = await Process.RunAsync("systemctl", $"-p ActiveState show {service}", token);
                        if (stateMatchesDesired(output.First().Split("=").Last()))
                        {
                            break;
                        }

                        await Task.Delay(250, token).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
