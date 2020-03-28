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
        Failed,
        Running,
        Stopped,
        Unknown
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

        public static Task WaitForStatusAsync(
            IEnumerable<EdgeModule> modules,
            EdgeModuleStatus desired,
            CancellationToken token)
        {
            string[] moduleIds = modules.Select(m => m.Id.TrimStart('$')).Distinct().ToArray();

            string FormatModulesList() => moduleIds.Length == 1 ? "Module '{0}'" : "Modules ({0})";

            async Task WaitForStatusAsync()
            {
                await Retry.Do(
                    async () =>
                    {
                        var localModules = await List(token);
                        return moduleIds.LongCount(moduleId =>
                            localModules.ContainsKey(moduleId) && localModules[moduleId].status == desired);
                    },
                    result => result == moduleIds.LongLength,
                    e => false,
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

        public static async Task<IReadOnlyDictionary<string, (EdgeModuleStatus status, string image)>> List(
            CancellationToken token)
        {
            var result = new Dictionary<string, (EdgeModuleStatus, string)>();

            try
            {
                string[] output = await Process.RunAsync("iotedge", "list", token);

                Log.Verbose(string.Join("\n", output));
				
                foreach (string line in output.Skip(1))
                {
                    string[] columns = line.Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
                    // each line is "name status description config"
                    result.Add(columns[0], (Enum.Parse<EdgeModuleStatus>(columns[1], ignoreCase: true), columns.Last()));
                }
            }
            catch (Exception e) when (
                e.ToString().Contains("Could not list modules", StringComparison.OrdinalIgnoreCase) ||
                e.ToString().Contains("Socket file could not be found", StringComparison.OrdinalIgnoreCase))
            {
                // iot edge list failed because iotedged's management endpoint is still starting up; treat it the same
                // as running with no modules
            }

            return result;
        }

        public async Task<bool> Matches(EdgeModuleStatus status, string image, CancellationToken token)
        {
            string moduleId = this.Id.TrimStart('$');
            var modules = await List(token);
            return modules.TryGetValue(moduleId, out (EdgeModuleStatus status, string image) m) &&
                status == m.status &&
                image == m.image;
        }

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
                    Twin twin = await this.iotHub.GetTwinAsync(this.deviceId, this.Id, token);
                    return twin.Properties.Reported;
                },
                reported => JsonEquals((expected, "properties.reported"), (reported, string.Empty)),
                null,
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
                // return all json values under root path, with their relative
                // paths as keys
                return JObject
                    .FromObject(obj)
                    .SelectToken(rootPath)
                    .Cast<JContainer>()
                    .DescendantsAndSelf()
                    .OfType<JValue>()
                    .ToDictionary(v => v.Path.Substring(rootPath.Length).TrimStart('.'));
            }

            Dictionary<string, JValue> referenceValues = ProcessJson(reference.obj, reference.rootPath);
            Dictionary<string, JValue> comparandValues = ProcessJson(comparand.obj, comparand.rootPath);

            // comparand equals reference if, for each json value in reference:
            // - comparand has a json value with the same path
            // - the json values match
            bool match = referenceValues.All(kvp => comparandValues.ContainsKey(kvp.Key) &&
                kvp.Value.Equals(comparandValues[kvp.Key]));

            if (!match)
            {
                IEnumerable<string> missing = referenceValues
                    .Where(kvp =>
                        !comparandValues.ContainsKey(kvp.Key) ||
                        !kvp.Value.Equals(comparandValues[kvp.Key]))
                    .Select(kvp => kvp.Key);
                Log.Verbose(
                    "Expected configuration values missing in agent's reported properties:\n  {MissingValues}",
                    string.Join("\n  ", missing));
            }

            return match;
        }
    }
}
