// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public enum EdgeModuleStatus
    {
        Running,
        Stopped
    }

    public class EdgeModule
    {
        protected string deviceId;
        protected IotHub iotHub;

        public EdgeModule(string id, string deviceId, IotHub iotHub)
        {
            this.deviceId = deviceId;
            this.iotHub = iotHub;
            this.Id = id;
        }

        public string Id { get; }

        public static Task WaitForStatusAsync(EdgeModule[] modules, EdgeModuleStatus desired, CancellationToken token)
        {
            (string template, string[] args) FormatModulesList() => modules.Length == 1
                ? ("module '{0}'", new[] { modules.First().Id })
                : ("modules ({0})", modules.Select(module => module.Id).ToArray());

            string SentenceCase(string input) =>
                $"{input.First().ToString().ToUpper()}{input.Substring(1)}";

            async Task WaitForStatusAsync()
            {
                await Retry.Do(
                    async () =>
                    {
                        string[] output = await Process.RunAsync("iotedge", "list", token);

                        Log.Verbose(string.Join("\n", output));

                        return output
                            .Where(
                                ln =>
                                {
                                    var columns = ln.Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var module in modules)
                                    {
                                        // each line is "name status"
                                        if (columns[0] == module.Id &&
                                            columns[1].Equals(desired.ToString(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            return true;
                                        }
                                    }

                                    return false;
                                }).ToArray();
                    },
                    a => a.Length == modules.Length,
                    e =>
                    {
                        // Retry if iotedged's management endpoint is still starting up,
                        // and therefore isn't responding to `iotedge list` yet
                        bool DaemonNotReady(string details) =>
                            details.Contains("Could not list modules", StringComparison.OrdinalIgnoreCase) ||
                            details.Contains("Socket file could not be found", StringComparison.OrdinalIgnoreCase);

                        return DaemonNotReady(e.ToString());
                    },
                    TimeSpan.FromSeconds(5),
                    token);
            }

            (string template, string[] args) = FormatModulesList();
            return Profiler.Run(
                WaitForStatusAsync,
                string.Format(SentenceCase(template), "{Modules}") + " entered the '{Desired}' state",
                string.Join(", ", args),
                desired.ToString().ToLower());
        }

        public Task WaitForStatusAsync(EdgeModuleStatus desired, CancellationToken token)
        {
            return WaitForStatusAsync(new[] { this }, desired, token);
        }

        public Task WaitForEventsReceivedAsync(CancellationToken token)
        {
            return Profiler.Run(
                () => this.iotHub.ReceiveEventsAsync(
                    this.deviceId,
                    data =>
                    {
                        data.SystemProperties.TryGetValue("iothub-connection-device-id", out object devId);
                        data.SystemProperties.TryGetValue("iothub-connection-module-id", out object modId);

                        return devId != null && devId.ToString().Equals(this.deviceId)
                                             && modId != null && modId.ToString().Equals(this.Id);
                    },
                    token),
                "Received events from device '{Device}' on Event Hub '{EventHub}'",
                this.deviceId,
                this.iotHub.EntityPath);
        }
    }
}
