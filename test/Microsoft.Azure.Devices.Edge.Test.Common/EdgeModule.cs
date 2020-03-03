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
            string[] moduleIds = modules.Select(m => m.Id.TrimStart('$')).Distinct().ToArray();

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

        public Task<string> WaitForEventsReceivedAsync(DateTime seekTime, CancellationToken token, params string[] requiredProperties)
        {
            return Profiler.Run(
                async () =>
                {
                    string resultBody = null;
                    await this.iotHub.ReceiveEventsAsync(
                        this.deviceId,
                        seekTime,
                        data =>
                        {
                            data.SystemProperties.TryGetValue("iothub-connection-device-id", out object devId);
                            data.SystemProperties.TryGetValue("iothub-connection-module-id", out object modId);

                            resultBody = Encoding.UTF8.GetString(data.Body);
                            Log.Verbose($"Received event for '{devId}/{modId}' with body '{resultBody}'");

                            return devId != null && devId.ToString().Equals(this.deviceId)
                                                  && modId != null && modId.ToString().Equals(this.Id)
                                                  && requiredProperties.All(data.Properties.ContainsKey);
                        },
                        token);

                    return resultBody;
                },
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

        public Task WaitForReportedPropertyUpdatesAsync(object expected, CancellationToken token)
        {
            return Profiler.Run(
                () => this.WaitForReportedPropertyUpdatesInternalAsync(expected, token),
                "Received expected twin updates for module '{Module}'",
                this.Id);
        }

        protected Task WaitForReportedPropertyUpdatesInternalAsync(object expected, CancellationToken token)
        {
            return Retry.Do(
                async () =>
                {
                    Twin twin = await this.iotHub.GetTwinAsync(this.deviceId, this.Id, token);
                    return twin.Properties.Reported;
                },
                reported => JsonEquals((expected, "properties.reported"), (reported, string.Empty)),
                null,
                TimeSpan.FromSeconds(5),
                token);
        }

        // reference.rootPath and comparand.rootPath are path strings like those returned from
        // Newtonsoft.Json.Linq.JToken.Path, and compatible with the path argument to
        // Newtonsoft.Json.Linq.JToken.SelectToken(path)
        static bool JsonEquals(
            (object obj, string rootPath) reference,
            (object obj, string rootPath) comparand)
        {
            // find the starting points of the comparison
            var rootRef = (JContainer)JObject
                .FromObject(reference.obj)
                .SelectToken(reference.rootPath);
            var rootCmp = (JContainer)JObject
                .FromObject(comparand.obj)
                .SelectToken(comparand.rootPath);

            // do an inner join on the leaf elements
            var descendantsRef = rootRef
                .DescendantsAndSelf()
                .Where(t => t is JValue)
                .Select(t => (JValue)t);
            var descendantsCmp = rootCmp
                .DescendantsAndSelf()
                .Where(t => t is JValue)
                .Select(t => (JValue)t);

            int pathLengthRef =
                reference.rootPath.Length + reference.rootPath == string.Empty ? 0 : 1;
            int pathLengthCmp =
                comparand.rootPath.Length + comparand.rootPath == string.Empty ? 0 : 1;

            var joined = descendantsRef.Join(
                descendantsCmp,
                v => v.Path.Substring(pathLengthRef),
                v => v.Path.Substring(pathLengthCmp),
                (v1, v2) => (v1, v2));

            // collect the paths of the subset of leaf elements whose values match
            var matches = joined
                .Where(values => values.Item1.Equals(values.Item2))
                .Select(values => values.Item1.Path.Substring(pathLengthRef));

            // comparand equals reference if subset has the same paths as reference
            Log.Verbose("Checking for expected configuration values in agent twin:");
            return descendantsRef
                .Select(d => d.Path.Substring(pathLengthRef))
                .All(
                    path =>
                    {
                        bool found = matches.Contains(path);
                        Log.Verbose("'{ValuePath}' found in twin? {Found}", path, found);
                        return found;
                    });
        }
    }
}
