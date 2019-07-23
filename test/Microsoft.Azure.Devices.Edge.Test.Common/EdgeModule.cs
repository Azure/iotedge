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

        public EdgeModule(string id, string deviceId)
        {
            this.deviceId = deviceId;
            this.Id = id;
        }

        public string Id { get; }

        public static Task WaitForStatusAsync(EdgeModule[] modules, EdgeModuleStatus desired, CancellationToken token)
        {
            string[] moduleIds = modules.Select(module => module.Id).Distinct().ToArray();

            string FormatModulesList() => moduleIds.Length == 1 ? "Module '{0}'" : "Modules ({0})";

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
                                    foreach (var moduleId in moduleIds)
                                    {
                                        // each line is "name status"
                                        if (columns[0] == moduleId &&
                                            columns[1].Equals(desired.ToString(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            return true;
                                        }
                                    }

                                    return false;
                                }).ToArray();
                    },
                    a => a.Length == moduleIds.Length,
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

            return Profiler.Run(
                WaitForStatusAsync,
                string.Format(FormatModulesList(), "{Modules}") + " entered the '{Desired}' state",
                string.Join(", ", moduleIds),
                desired.ToString().ToLower());
        }

        public Task WaitForStatusAsync(EdgeModuleStatus desired, CancellationToken token)
        {
            return WaitForStatusAsync(new[] { this }, desired, token);
        }

        public Task WaitForEventsReceivedAsync(DateTime seekTime, IotHub iotHub, CancellationToken token)
        {
            return Profiler.Run(
                () => iotHub.ReceiveEventsAsync(
                    this.deviceId,
                    seekTime,
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
                iotHub.EntityPath);
        }
    }
}
