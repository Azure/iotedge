// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;

    public class AvailabilityMetrics : IAvailabilityMetric, IDisposable
    {
        readonly IMetricsGauge running;
        readonly IMetricsGauge expectedRunning;
        readonly ISystemTime systemTime;
        readonly ILogger log = Logger.Factory.CreateLogger<AvailabilityMetrics>();

        // This allows edgeAgent to track its own avaliability. If edgeAgent shutsdown unexpectedly, it can look at the last checkpoint time to determine its previous avaliability.
        readonly TimeSpan checkpointFrequency = TimeSpan.FromMinutes(5);
        readonly PeriodicTask updateCheckpointFile;
        readonly string checkpointFile;

        readonly List<Availability> availabilities;
        readonly Lazy<Availability> edgeAgent;

        public AvailabilityMetrics(IMetricsProvider metricsProvider, string storageFolder, ISystemTime time = null)
        {
            this.systemTime = time ?? SystemTime.Instance;
            this.availabilities = new List<Availability>();
            this.edgeAgent = new Lazy<Availability>(() => new Availability(Constants.EdgeAgentModuleName, this.CalculateEdgeAgentDowntime(), this.systemTime));

            Preconditions.CheckNotNull(metricsProvider, nameof(metricsProvider));
            this.running = metricsProvider.CreateGauge(
                "total_time_running_correctly_seconds",
                "The amount of time the module was specified in the deployment and was in the running state",
                new List<string> { "module_name" });

            this.expectedRunning = metricsProvider.CreateGauge(
                "total_time_expected_running_seconds",
                "The amount of time the module was specified in the deployment",
                new List<string> { "module_name" });

            string storageDirectory = Path.Combine(Preconditions.CheckNonWhiteSpace(storageFolder, nameof(storageFolder)), "availability");
            try
            {
                Directory.CreateDirectory(storageDirectory);
                this.checkpointFile = Path.Combine(storageDirectory, "avaliability.checkpoint");
                this.updateCheckpointFile = new PeriodicTask(this.UpdateCheckpointFile, this.checkpointFrequency, this.checkpointFrequency, this.log, "Checkpoint Availability");
            }
            catch (Exception ex)
            {
                this.log.LogError(ex, "Could not create checkpoint directory");
            }
        }

        public void ComputeAvailability(ModuleSet desired, ModuleSet current)
        {
            IEnumerable<IRuntimeModule> modulesToCheck = current.Modules.Values
                .OfType<IRuntimeModule>()
                .Where(m => m.Name != Constants.EdgeAgentModuleName);

            /* Get all modules that are not running but should be */
            var down = new HashSet<string>(modulesToCheck
                .Where(m =>
                    m.RuntimeStatus != ModuleStatus.Running &&
                    desired.Modules.TryGetValue(m.Name, out var d) &&
                    d.DesiredStatus == ModuleStatus.Running)
                .Select(m => m.Name));

            /* Get all correctly running modules */
            var up = new HashSet<string>(modulesToCheck
                .Where(m => m.RuntimeStatus == ModuleStatus.Running)
                .Select(m => m.Name));

            /* handle edgeAgent specially */
            this.edgeAgent.Value.AddPoint(true);
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
                this.availabilities.Add(new Availability(module, this.systemTime));
            }
        }

        public void Dispose()
        {
            this.updateCheckpointFile.Dispose();
        }

        /*
         * The Logic below handles edgeAgent's own avaliability. It keeps a checkpoint file containing the current timestamp
         *  that it updates every 5 minutes. On a clean shutdown, it deletes this file to indicate it shouldn't be running. If agent crashes
         *  or returns a non-zero code, it leaves the file. On startup, if the file exists, agent knows it shutdown incorrectly \
         *  and can calculate its downtime using the timestamp in the file.
         */
        public void IndicateCleanShutdown()
        {
            try
            {
                File.Delete(this.checkpointFile);
            }
            catch (Exception ex)
            {
                this.log.LogError(ex, "Could not delete checkpoint file");
            }
        }

        TimeSpan CalculateEdgeAgentDowntime()
        {
            try
            {
                if (File.Exists(this.checkpointFile))
                {
                    long ticks = long.Parse(File.ReadAllText(this.checkpointFile));
                    DateTime checkpointTime = new DateTime(ticks, DateTimeKind.Utc);
                    return this.systemTime.UtcNow - checkpointTime;
                }
            }
            catch (Exception ex)
            {
                this.log.LogError(ex, "Could not load shutdown time");
            }

            return TimeSpan.Zero;
        }

        Task UpdateCheckpointFile()
        {
            File.WriteAllText(this.checkpointFile, this.systemTime.UtcNow.Ticks.ToString());
            return Task.CompletedTask;
        }
    }
}
