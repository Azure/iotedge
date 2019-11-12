// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;

    public interface IAvailabilityMetric
    {
        void ComputeAvailability(ModuleSet desired, ModuleSet current);
        void OnCleanShutdown();
    }

    public class AvailabilityMetrics : IAvailabilityMetric, IDisposable
    {
        readonly IMetricsGauge running;
        readonly IMetricsGauge expectedRunning;
        readonly ISystemTime time;
        readonly ILogger log = Logger.Factory.CreateLogger<Availability>();

        // This allows edgeAgent to track its own avaliability. If edgeAgent shutsdown unexpectedly, it can look at the last checkpoint time to determine its previous avaliability.
        readonly TimeSpan checkpointFrequency = TimeSpan.FromMinutes(5);
        readonly PeriodicTask checkpoint;

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

            this.checkpoint = new PeriodicTask(this.NoteCurrentTime, this.checkpointFrequency, this.checkpointFrequency, this.log, "Checkpoint Availability");
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

        public void Dispose()
        {
            this.checkpoint.Dispose();
        }

        public void OnCleanShutdown()
        {
            try
            {
                File.Delete("shutdown_time");
            }
            catch (Exception ex)
            {
                this.log.LogError($"Could not delete checkpoint file:\n{ex}");
            }
        }

        TimeSpan CalculateEdgeAgentDowntime()
        {
            try
            {
                if (File.Exists("avaliability_checkpoint"))
                {
                    long ticks = long.Parse(File.ReadAllText("shutdown_time"));
                    DateTime checkpointTime = new DateTime(ticks);
                    return this.time.UtcNow - checkpointTime;
                }
            }
            catch (Exception ex)
            {
                this.log.LogError($"Could not load shutdown time:\n{ex}");
            }

            return TimeSpan.Zero;
        }

        Task NoteCurrentTime()
        {
            File.WriteAllText("avaliability_checkpoint", this.time.UtcNow.Ticks.ToString());
            return Task.CompletedTask;
        }
    }
}
