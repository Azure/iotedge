// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class Platform
    {
        public static async Task<IEnumerable<string>> CollectLogsAsync(DateTime testStartTime, string filePrefix, CancellationToken token)
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
            string eventLogCommand =
                "Get-WinEvent -ErrorAction SilentlyContinue " +
                $"-FilterHashtable @{{ProviderName='iotedged';LogName='application';StartTime='{testStartTime}'}} " +
                "| Select TimeCreated, Message " +
                "| Sort-Object @{Expression=\'TimeCreated\';Descending=$false} " +
                "| Format-Table -AutoSize -HideTableHeaders " +
                "| Out-String -Width 512";

            string daemonLog = $"{filePrefix}-iotedged.log";
            output = await Process.RunAsync("powershell", eventLogCommand, token);
            await File.WriteAllLinesAsync(daemonLog, output, token);
            paths.Add(daemonLog);

            return paths;
        }

        // TODO: download installer script from aka.ms if user doesn't pass installerPath in Windows
        public static IEdgeDaemon CreateEdgeDaemon(Option<string> installerPath) => IsWindows()
            ? new Windows.EdgeDaemon(installerPath.Expect(() => new ArgumentException()))
            : new Linux.EdgeDaemon() as IEdgeDaemon;

        public static string GetConfigYamlPath() => IsWindows()
            ? @"C:\ProgramData\iotedge\config.yaml"
            : "/etc/iotedge/config.yaml";

        public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
}
