// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Reporters
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    public class IoTHubReporter : IReporter
    {
        readonly ITwinConfigSource twinConfigSource;
        readonly IDeviceClient deviceClient;
        readonly object sync;
        Option<ModuleSet> reported;

        public IoTHubReporter(IDeviceClient deviceClient, ITwinConfigSource twinConfigSource)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.twinConfigSource = Preconditions.CheckNotNull(twinConfigSource, nameof(twinConfigSource));
            this.sync = new object();
            this.reported = Option.None<ModuleSet>();
        }

        ModuleSet Reported
        {
            // if we have a cached copy of the reported modules list then we return that;
            // if we don't have it cached then we return whatever we got when we fetched the
            // twin from IoT Hub via twinConfigSource
            get
            {
                lock (this.sync)
                {
                    ModuleSet reported = this.reported.GetOrElse(this.twinConfigSource.ReportedModuleSet);
                    return reported.Equals(ModuleSet.Empty) ? reported : new ModuleSet(reported.Modules);
                }
            }

            set
            {
                lock (this.sync)
                {
                    this.reported = Option.Some(value);
                }
            }
        }

        public async Task ReportAsync(ModuleSet moduleSet)
        {
            Diff diff = moduleSet.Diff(this.Reported);

            // add the modules that are still running
            var modulesMap = new Dictionary<string, IModule>(diff.Updated.ToImmutableDictionary(m => m.Name));

            // add removed modules by assigning 'null' as the value
            foreach (string moduleName in diff.Removed)
            {
                modulesMap.Add(moduleName, null);
            }

            if (modulesMap.Count > 0)
            {
                var reportedProps = new TwinCollection
                {
                    ["modules"] = modulesMap
                };

                try
                {
                    await this.deviceClient.UpdateReportedPropertiesAsync(reportedProps);

                    // update our cached copy of reported properties
                    this.Reported = moduleSet;

                    Events.UpdatedReportedProperties();
                }
                catch (Exception e)
                {
                    Events.UpdateReportedPropertiesFailed(e);

                    // Swallow the exception as the device could be offline. The reported properties will get updated
                    // during the next reconcile when we have connectivity.
                }
            }
        }
    }

    static class Events
    {
        static readonly ILogger Log = Util.Logger.Factory.CreateLogger<IoTHubReporter>();
        const int IdStart = AgentEventIds.IoTHubReporter;

        enum EventIds
        {
            UpdateReportedPropertiesFailed = IdStart,
            UpdatedReportedProperties = IdStart + 1
        }

        public static void UpdatedReportedProperties()
        {
            Log.LogInformation((int)EventIds.UpdatedReportedProperties, $"Updated reported properties");
        }

        public static void UpdateReportedPropertiesFailed(Exception e)
        {
            Log.LogWarning((int)EventIds.UpdateReportedPropertiesFailed, $"Updating reported properties failed with error {e.Message} type {e.GetType()}");
        }
    }
}
