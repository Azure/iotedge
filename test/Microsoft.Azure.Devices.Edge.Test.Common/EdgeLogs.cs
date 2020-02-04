// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public static class EdgeLogs
    {
        public static async Task<IEnumerable<string>> CollectAsync(DateTime testStartTime, string filePrefix, CancellationToken token)
        {
            var paths = new List<string>();

            // Save module logs
            string[] output = await Process.RunAsync("iotedge", "list", token);
            string[] modules = output.Select(ln => ln.Split(null as char[], StringSplitOptions.RemoveEmptyEntries).First()).Skip(1).ToArray();

            foreach (string name in modules)
            {
                string moduleLog = $"{filePrefix}-{name}.log";
                output = await Process.RunAsync("iotedge", $"logs {name}", token);
                await File.WriteAllLinesAsync(moduleLog, output, token);
                paths.Add(moduleLog);
            }

            // Save daemon logs
            string daemonLog = await OsPlatform.Current.CollectDaemonLogsAsync(testStartTime, filePrefix, token);
            paths.Add(daemonLog);

            return paths;
        }
    }
}
