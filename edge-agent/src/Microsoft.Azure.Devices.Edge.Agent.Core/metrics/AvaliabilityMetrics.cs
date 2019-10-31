// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;

    public interface IAvailabilityMetric
    {
        void ComputeAvailability(ModuleSet desired, ModuleSet current);
    }

    public class AvailabilityMetrics : IAvailabilityMetric
    {
        readonly IMetricsGauge running;
        readonly IMetricsGauge expectedRunning;
        readonly ISystemTime time;
        readonly ILogger log = Logger.Factory.CreateLogger<Availability>();

        readonly List<Availability> availabilities;
        readonly Lazy<Availability> edgeAgent;

        public AvailabilityMetrics(IMetricsProvider metricsProvider, ISystemTime time)
        {
            this.running = metricsProvider.CreateGauge(
                "total_time_running_correctly_seconds",
                "The amount of time the module was specified in the deployment and was in the running state",
                new List<string> { "module_name" });

            this.expectedRunning = metricsProvider.CreateGauge(
                "total_time_expected_running_seconds",
                "The amount of time the module was specified in the deployment",
                new List<string> { "module_name" });

            this.time = time;
            this.availabilities = new List<Availability>();
            this.edgeAgent = new Lazy<Availability>(() => new Availability("edgeAgent", this.CalculateEdgeAgentDowntime(), this.time));
        }

        public void ComputeAvailability(ModuleSet desired, ModuleSet current)
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
            this.edgeAgent.Value.AddPoint(true);
            down.Remove("edgeAgent");
            up.Remove("edgeAgent");
            this.running.Set(this.edgeAgent.Value.ExpectedTime.TotalSeconds, new[] { this.edgeAgent.Value.Name });
            this.expectedRunning.Set(this.edgeAgent.Value.RunningTime.TotalSeconds, new[] { this.edgeAgent.Value.Name });

            /* Add points for all other modules found */
            foreach (Availability availability in this.availabilities)
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

                this.running.Set(availability.RunningTime.TotalSeconds, new[] { availability.Name });
                this.expectedRunning.Set(availability.ExpectedTime.TotalSeconds, new[] { availability.Name });
            }

            /* Add new modules to track */
            foreach (string module in down.Union(up))
            {
                this.availabilities.Add(new Availability(module, this.time));
            }
        }

        TimeSpan CalculateEdgeAgentDowntime()
        {
            AppDomain.CurrentDomain.ProcessExit += this.NoteCurrentTime;
            try
            {
                if (File.Exists("shutdown_time"))
                {
                    // TODO: get iotedged uptime. if < a couple minutes, assume intentional shutdown and return 0.
                    long ticks = long.Parse(File.ReadAllText("shutdown_time"));
                    return this.time.UtcNow - new DateTime(ticks);
                }
            }
            catch (Exception ex)
            {
                this.log.LogError($"Could not load shutdown time:\n{ex}");
            }

            return TimeSpan.Zero;
        }

        void NoteCurrentTime(object sender, EventArgs e)
        {
            try
            {
                File.WriteAllText("shutdown_time", this.time.UtcNow.Ticks.ToString());
            }
            catch (Exception ex)
            {
                this.log.LogError($"Could not save shutdown time:\n{ex}");
            }
        }
    }
}
