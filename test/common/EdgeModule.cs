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

        public async Task WaitForStatusAsync(EdgeModuleStatus desired, CancellationToken token)
        {
            try
            {
                await Retry.Do(
                    async () =>
                    {
                        string[] result = await Process.RunAsync("iotedge", "list", token);

                        return result
                            .Where(ln => ln.Split(null as char[], StringSplitOptions.RemoveEmptyEntries).First() == this.name)
                            .DefaultIfEmpty("name status")
                            .Single()
                            .Split(null as char[], StringSplitOptions.RemoveEmptyEntries)
                            .ElementAt(1); // second column is STATUS
                    },
                    s => desired.ToString().Equals(s, StringComparison.OrdinalIgnoreCase),
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
                throw new Exception($"Error searching for {this.name} module: timed out waiting for module to start");
            }
            catch (Exception e)
            {
                throw new Exception($"Error searching for {this.name} module: {e}");
            }

            Console.WriteLine($"Edge module '{this.name}' is running");
        }
    }
}