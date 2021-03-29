// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor {
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    internal class ResourceUsageTelemetryReporter {
        private readonly PeriodicTask periodicMeasureAndSend;

        // Apparently Process.GetCurrentProcess() can be expensive. Initialize myProcess lazily to not slow down module startup.
        private readonly Lazy<Process> myProcess = new Lazy<Process>(() => Process.GetCurrentProcess());
        private bool cpuMeasurementTaken = false;
        private DateTime lastProcessorMeasurementTime = DateTime.UnixEpoch;
        private TimeSpan lastProcessorTime = TimeSpan.FromSeconds(0);

        public ResourceUsageTelemetryReporter(ILogger logger) {
            this.periodicMeasureAndSend = new PeriodicTask(this.DoTask, System.TimeSpan.FromSeconds(60), System.TimeSpan.FromSeconds(20), logger, "Capture performance telemetry data");
        }

        private async Task DoTask(CancellationToken cancellationToken) {
            var nextProcessorTime = myProcess.Value.TotalProcessorTime;
            var nextTime = DateTime.Now;
            if (cpuMeasurementTaken && nextTime != lastProcessorMeasurementTime) {
                 // a previous CPU time measurement has been taken, we can calculate CPU usage.
                double cpuUsage = (nextProcessorTime - lastProcessorTime) / (nextTime - lastProcessorMeasurementTime) * 1000;  // * 1000 to get millicores
            }
            else {
                // take a first CPU time measurement
                lastProcessorMeasurementTime = DateTime.Now;
                lastProcessorTime = myProcess.Value.TotalProcessorTime;
                cpuMeasurementTaken = true;
            }

            // make this function async, PeriodicTask requires it
            await Task.Delay(TimeSpan.FromSeconds(0));
        }
    }
}
