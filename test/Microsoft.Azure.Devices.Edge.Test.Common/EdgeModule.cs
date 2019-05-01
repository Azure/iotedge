// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

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
            string FormatModulesList() => modules.Length == 1
                ? $"module '{modules.First().Id}'"
                : $"modules ({string.Join(", ", modules.Select(module => module.Id))})";

            async Task WaitForStatusAsync()
            {
                try
                {
                    await Retry.Do(
                        async () =>
                        {
                            string[] result = await Process.RunAsync("iotedge", "list", token);

                            return result
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
                catch (OperationCanceledException)
                {
                    throw new Exception($"Error: timed out waiting for {FormatModulesList()} to start");
                }
                catch (Exception e)
                {
                    throw new Exception($"Error searching for {FormatModulesList()}: {e}");
                }
            }

            return Profiler.Run(
                $"Waiting for {FormatModulesList()} to enter the '{desired.ToString().ToLower()}' state",
                WaitForStatusAsync);
        }

        public Task WaitForStatusAsync(EdgeModuleStatus desired, CancellationToken token)
        {
            return WaitForStatusAsync(new[] { this }, desired, token);
        }

        public Task WaitForEventsReceivedAsync(CancellationToken token)
        {
            return Profiler.Run(
                $"Receiving events from device '{this.deviceId}' on Event Hub '{this.iotHub.EntityPath}'",
                () => this.iotHub.ReceiveEventsAsync(
                    this.deviceId,
                    data =>
                    {
                        data.SystemProperties.TryGetValue("iothub-connection-device-id", out object devId);
                        data.SystemProperties.TryGetValue("iothub-connection-module-id", out object modId);

                        return devId != null && devId.ToString().Equals(this.deviceId)
                                             && modId != null && modId.ToString().Equals(this.Id);
                    },
                    token));
        }
    }
}
