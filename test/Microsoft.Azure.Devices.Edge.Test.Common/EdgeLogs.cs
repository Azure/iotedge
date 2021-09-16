// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;

    public static class EdgeLogs
    {
        // Make an effort to collect logs, but swallow any exceptions to prevent tests/fixtures
        // from failing if this function fails.
        public static async Task<IEnumerable<string>> CollectAsync(DateTime testStartTime, string filePrefix, CancellationToken token)
        {
            var paths = new List<string>();

            // Save module logs
            try
            {
                string[] output = await Process.RunAsync("iotedge", "list", token);
                string[] modules = output.Select(ln => ln.Split(null as char[], StringSplitOptions.RemoveEmptyEntries).First()).Skip(1).ToArray();

                foreach (string name in modules)
                {
                    string moduleLog = $"{filePrefix}-{name}.log";
                    output = await Process.RunAsync("iotedge", $"logs {name}", token, logVerbose: false);
                    await File.WriteAllLinesAsync(moduleLog, output, token);
                    paths.Add(moduleLog);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"Failed to collect module logs\nexception: {e.ToString()}");
            }

            // Save daemon logs
            try
            {
                string daemonLog = await OsPlatform.Current.CollectDaemonLogsAsync(testStartTime, filePrefix, token);
                paths.Add(daemonLog);
            }
            catch (Exception e)
            {
                Log.Warning($"Failed to collect daemon logs\nexception: {e.ToString()}");
            }

            return paths;
        }
    }
}
