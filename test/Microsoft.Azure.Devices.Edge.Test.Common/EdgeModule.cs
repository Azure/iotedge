// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;
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
                                    string[] columns = ln.Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
                                    foreach (string moduleId in moduleIds)
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
                        static bool DaemonNotReady(string details) =>
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

        public Task WaitForStatusAsync(EdgeModuleStatus desired, CancellationToken token) =>
            WaitForStatusAsync(new[] { this }, desired, token);

        public Task<string> WaitForEventsReceivedAsync(
            DateTime seekTime,
            CancellationToken token,
            params string[] requiredProperties) => Profiler.Run(
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

        public Task UpdateDesiredPropertiesAsync(object patch, CancellationToken token) => Profiler.Run(
            () => this.iotHub.UpdateTwinAsync(this.deviceId, this.Id, patch, token),
            "Updated twin for module '{Module}'",
            this.Id);

        public Task WaitForReportedPropertyUpdatesAsync(object expected, CancellationToken token) => Profiler.Run(
            () => this.WaitForReportedPropertyUpdatesInternalAsync(expected, token),
            "Received expected twin updates for module '{Module}'",
            this.Id);

        protected Task WaitForReportedPropertyUpdatesInternalAsync(object expected, CancellationToken token) =>
            Retry.Do(
                async () =>
                {
                    Twin twin = await this.iotHub.GetTwinAsync(this.deviceId, Option.Some(this.Id), token);
                    return twin.Properties.Reported;
                },
                reported => JsonEquals((expected, "properties.reported"), (reported, string.Empty)),
                // Ignore key not found Exception. There can be a delay between deployement on device and reported state, especially in nested configuration
                e =>
                {
                    if (e is KeyNotFoundException)
                    {
                        Log.Information("The device has not yet repported all the keys, retrying:" + e);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(5),
                token);

        // reference.rootPath and comparand.rootPath are path strings like those returned from
        // Newtonsoft.Json.Linq.JToken.Path, and compatible with the path argument to
        // Newtonsoft.Json.Linq.JToken.SelectToken(path)
        static bool JsonEquals(
            (object obj, string rootPath) reference,
            (object obj, string rootPath) comparand)
        {
            Dictionary<string, JValue> ProcessJson(object obj, string rootPath)
            {
                // return all json values under root path, with their relative paths as keys
                Dictionary<string, JValue> result = JObject
                    .FromObject(obj)
                    .SelectToken(rootPath)
                    .Cast<JContainer>()
                    .DescendantsAndSelf()
                    .OfType<JValue>()
                    .ToDictionary(v => v.Path.Substring(rootPath.Length).TrimStart('.'));

                var agentKeys = result.Keys
                    .Where(k => k.EndsWith("edgeAgent.settings.createOptions"));
                var otherKeys = result.Keys
                    .Where(k => k.EndsWith("settings.createOptions"))
                    .Except(agentKeys);

                // normalize stringized JSON inside "createOptions"
                foreach (var key in otherKeys)
                {
                    result[key].Value = JObject
                        .Parse((string)result[key].Value)
                        .ToString(Formatting.None);
                }

                // Do some additional processing for edge agent's createOptions...
                // Remove "net.azure-devices.edge.*" labels because they can be deeply nested
                // stringized JSON, making them difficult to compare. Besides, they're created by
                // edge agent and iotedged for internal use; they're not related to the deployment.
                foreach (var key in agentKeys)
                {
                    JObject createOptions = JObject.Parse((string)result[key].Value);
                    if (createOptions.TryGetValue("Labels", out JToken labels))
                    {
                        string[] remove = labels
                            .Children<JProperty>()
                            .Where(label => label.Name.StartsWith("net.azure-devices.edge."))
                            .Select(label => label.Name)
                            .ToArray();
                        foreach (var name in remove)
                        {
                            labels.Value<JObject>().Remove(name);
                        }

                        if (!labels.HasValues)
                        {
                            createOptions.Remove("Labels");
                        }
                    }

                    result[key].Value = createOptions.ToString(Formatting.None);
                }

                var imagesKeys = result.Keys
                    .Where(k => k.EndsWith("settings.image"));
                foreach (var imageKeys in imagesKeys)
                {
                    result[imageKeys].Value = Regex.Replace((string)result[imageKeys].Value, ".*?/(.*)", m => m.Groups[1].Value);
                }

                return result;
            }

            Dictionary<string, JValue> referenceValues = ProcessJson(reference.obj, reference.rootPath);
            Dictionary<string, JValue> comparandValues = ProcessJson(comparand.obj, comparand.rootPath);

            // comparand equals reference if, for each json value in reference:
            // - comparand has a json value with the same path
            // - the json values match
            bool match = referenceValues.All(
                kvp =>
                {
                    return comparandValues.ContainsKey(kvp.Key) &&
                        kvp.Value.Equals(comparandValues[kvp.Key]);
                });

            if (!match)
            {
                string[] missing = referenceValues
                    .Where(kvp => !comparandValues.ContainsKey(kvp.Key))
                    .Select(kvp => kvp.Key)
                    .ToArray();
                if (missing.Length != 0)
                {
                    Log.Verbose(
                        "Expected configuration values missing in agent's reported properties:\n  {MissingValues}",
                        string.Join("\n  ", missing));
                }

                string[] different = referenceValues
                    .Where(kvp => comparandValues.ContainsKey(kvp.Key) && !kvp.Value.Equals(comparandValues[kvp.Key]))
                    .Select(kvp => $"{kvp.Key}: '{kvp.Value}' != '{comparandValues[kvp.Key]}'")
                    .ToArray();
                if (different.Length != 0)
                {
                    Log.Verbose(
                        "Expected configuration values don't match agent's reported properties:\n  {DifferentValues}",
                        string.Join("\n  ", different));
                }
            }

            return match;
        }
    }
}
