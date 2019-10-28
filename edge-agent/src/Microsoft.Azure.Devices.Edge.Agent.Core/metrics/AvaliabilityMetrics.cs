// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;

    static class AvailabilityMetrics
    {
        private static readonly IMetricsGauge Running = Util.Metrics.Metrics.Instance.CreateGauge(
            "total_time_running_correctly_seconds",
            "The amount of time the module was specified in the deployment and was in the running state",
            new List<string> { "module_name", "module_version" });

        private static readonly IMetricsGauge ExpectedRunning = Util.Metrics.Metrics.Instance.CreateGauge(
            "total_time_expected_running_seconds",
            "The amount of time the module was specified in the deployment",
            new List<string> { "module_name", "module_version" });

        public static ISystemTime Time = SystemTime.Instance;

        private static List<Availability> availabilities = new List<Availability>();
        private static Availability edgeAgent = new Availability("edgeAgent", "tempNoVersion", Time);

        public static void ComputeAvailability(ModuleSet desired, ModuleSet current)
        {
            /* Get all modules that are not running but should be */
            var down = new HashSet<string>(current.Modules.Values
                .Where(c =>
                    (c is IRuntimeModule) &&
                    (c as IRuntimeModule).RuntimeStatus != ModuleStatus.Running &&
                    desired.Modules.TryGetValue(c.Name, out var d) &&
                    d.DesiredStatus == ModuleStatus.Running)
                .Select(c => c.Name));

            /* Get all correctly running modules */
            var up = new HashSet<string>(current.Modules.Values
                .Where(c => (c is IRuntimeModule) && (c as IRuntimeModule).RuntimeStatus == ModuleStatus.Running)
                .Select(c => c.Name));

            /* handle edgeAgent specially */
            edgeAgent.AddPoint(true);
            down.Remove("edgeAgent");
            up.Remove("edgeAgent");
            Running.Set(edgeAgent.ExpectedTime.TotalSeconds, new[] { edgeAgent.Name, edgeAgent.Version });
            ExpectedRunning.Set(edgeAgent.RunningTime.TotalSeconds, new[] { edgeAgent.Name, edgeAgent.Version });

            /* Add points for all other modules found */
            foreach (Availability availability in availabilities)
            {
                if (down.Remove(availability.Name))
                {
                    availability.AddPoint(false);
                }
                else if (up.Remove(availability.Name))
                {
                    availability.AddPoint(true);
                }
                else
                {
                    /* stop calculating if in intentional stopped state or not deployed */
                    availability.NoPoint();
                }

                Running.Set(availability.RunningTime.TotalSeconds, new[] { availability.Name, availability.Version });
                ExpectedRunning.Set(availability.ExpectedTime.TotalSeconds, new[] { availability.Name, availability.Version });
            }

            /* Add new modules to track */
            foreach (string module in down.Union(up))
            {
                availabilities.Add(new Availability(module, "tempNoVersion", Time));
            }
        }
    }
}
