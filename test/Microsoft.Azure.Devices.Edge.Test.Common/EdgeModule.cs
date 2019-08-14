// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;
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

        public string Id { get; }

        public EdgeModule(string id, string deviceId, IotHub iotHub)
        {
            this.deviceId = deviceId;
            this.Id = id;
            this.iotHub = iotHub;
        }

        public static Task WaitForStatusAsync(IEnumerable<EdgeModule> modules, EdgeModuleStatus desired, CancellationToken token)
        {
            string[] moduleIds = modules.Select(m => m.Id).Distinct().ToArray();

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

        public Task WaitForEventsReceivedAsync(DateTime seekTime, CancellationToken token)
        {
            return Profiler.Run(
                () => this.iotHub.ReceiveEventsAsync(
                    this.deviceId,
                    seekTime,
                    data =>
                    {
                        data.SystemProperties.TryGetValue("iothub-connection-device-id", out object devId);
                        data.SystemProperties.TryGetValue("iothub-connection-module-id", out object modId);

                        Log.Verbose($"Received event for '{devId}/{modId}' with body '{Encoding.UTF8.GetString(data.Body)}'");

                        return devId != null && devId.ToString().Equals(this.deviceId)
                                             && modId != null && modId.ToString().Equals(this.Id);
                    },
                    token),
                "Received events from device '{Device}' on Event Hub '{EventHub}'",
                this.deviceId,
                this.iotHub.EntityPath);
        }

        public Task UpdateDesiredPropertiesAsync(object patch, CancellationToken token)
        {
            return Profiler.Run(
                () => this.iotHub.UpdateTwinAsync(this.deviceId, this.Id, patch, token),
                "Updated twin for module '{Module}'",
                this.Id);
        }

        public Task WaitForReportedPropertyUpdatesAsync(object expectedPatch, CancellationToken token)
        {
            return Profiler.Run(
                () =>
                {
                    return Retry.Do(
                        async () =>
                        {
                            Twin twin = await this.iotHub.GetTwinAsync(this.deviceId, this.Id, token);
                            return twin.Properties.Reported;
                        },
                        reported =>
                        {
                            JObject expected = JObject.FromObject(expectedPatch)
                                .Value<JObject>("properties")
                                .Value<JObject>("reported");
                            return expected.Value<JObject>().All<KeyValuePair<string, JToken>>(
                                prop => reported.Contains(prop.Key) && reported[prop.Key] == prop.Value);
                        },
                        null,
                        TimeSpan.FromSeconds(5),
                        token);
                },
                "Received expected twin updates for module '{Module}'",
                this.Id);
        }
    }
}
