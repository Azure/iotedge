// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Edge.Util;

namespace common
{
    public enum EdgeModuleStatus
    {
        Running,
        Stopped
    }

    public class EdgeModule
    {
        string name;

        public EdgeModule(string name)
        {
            this.name = name;
        }

        public Task WaitForStatusAsync(EdgeModuleStatus desired, CancellationToken token)
        {
            return EdgeModule.WaitForStatusAsync(new []{this}, desired, token);
        }

        public static async Task WaitForStatusAsync(EdgeModule[] modules, EdgeModuleStatus desired, CancellationToken token)
        {
            string FormatModulesList() => modules.Length == 1
                ? $"module '{modules.First().name}'"
                : $"modules ({String.Join(", ", modules.Select(module => module.name))})";

            string FormatSuccessMessage() => $"Edge {FormatModulesList()} " +
                (modules.Length == 1 ? "is running" : "are running");

            try
            {
                await Retry.Do(
                    async () =>
                    {
                        string[] result = await Process.RunAsync("iotedge", "list", token);

                        return result
                            .Where(ln => {
                                var columns = ln.Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
                                foreach (var module in modules)
                                {
                                    // each line is "name status"
                                    if (columns[0] == module.name &&
                                        columns[1].Equals(desired.ToString(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        return true;
                                    }
                                }
                                return false;
                            }).ToArray();
                    },
                    a => a.Length == modules.Length,
                    e =>
                    {
                        // Retry if iotedged's management endpoint is still starting up,
                        // and therefore isn't responding to `iotedge list` yet
                        bool DaemonNotReady(string details) =>
                            details.Contains("Could not list modules", StringComparison.OrdinalIgnoreCase) ||
                            details.Contains("Socket file could not be found", StringComparison.OrdinalIgnoreCase);
                        return DaemonNotReady(e.ToString()) ? true : false;
                    },
                    TimeSpan.FromSeconds(5),
                    token);
            }
            catch (OperationCanceledException)
            {
                throw new Exception($"Error: timed out waiting for {FormatModulesList()} to start");
            }
            catch (Exception e)
            {
                throw new Exception($"Error searching for {FormatModulesList()}: {e}");
            }

            Console.WriteLine(FormatSuccessMessage());
        }
    }
}