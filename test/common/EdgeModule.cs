// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Azure.EventHubs;
using EventHubTransportType = Microsoft.Azure.EventHubs.TransportType;

namespace common
{
    public enum EdgeModuleStatus
    {
        Running,
        Stopped
    }

    public class EdgeModule
    {
        protected DeviceContext context;
        string name;

        public EdgeModule(string name, EdgeDevice device)
        {
            this.context = device.Context;
            this.name = name;
        }

        public Task WaitForStatusAsync(EdgeModuleStatus desired, CancellationToken token)
        {
            return EdgeModule.WaitForStatusAsync(new []{this}, desired, token);
        }

        public static Task WaitForStatusAsync(EdgeModule[] modules, EdgeModuleStatus desired, CancellationToken token)
        {
            string FormatModulesList() => modules.Length == 1
                ? $"module '{modules.First().name}'"
                : $"modules ({String.Join(", ", modules.Select(module => module.name))})";

            async Task _WaitForStatusAsync() {
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
                        token
                    );
                }
                catch (OperationCanceledException)
                {
                    throw new Exception($"Error: timed out waiting for {FormatModulesList()} to start");
                }
                catch (Exception e)
                {
                    throw new Exception($"Error searching for {FormatModulesList()}: {e}");
                }
            }

            return Profiler.Run(
                $"Waiting for {FormatModulesList()} to enter the '{desired.ToString().ToLower()}' state",
                _WaitForStatusAsync
            );
        }

        public Task ReceiveEventsAsync(string eventHubConnectionString, CancellationToken token)
        {
            var builder = new EventHubsConnectionStringBuilder(eventHubConnectionString)
            {
                TransportType = EventHubTransportType.AmqpWebSockets
            };

            string deviceId = this.context.Device.Id;

            async Task _ReceiveEventsAsync()
            {
                EventHubClient client = EventHubClient.CreateFromConnectionString(builder.ToString());
                int count = (await client.GetRuntimeInformationAsync()).PartitionCount;
                string partition = EventHubPartitionKeyResolver.ResolveToPartition(deviceId, count);
                PartitionReceiver receiver = client.CreateReceiver("$Default", partition, EventPosition.FromEnd());

                var result = new TaskCompletionSource<bool>();
                using (token.Register(() => result.TrySetCanceled()))
                {
                    receiver.SetReceiveHandler(
                        new PartitionReceiveHandler(
                            data =>
                            {
                                data.SystemProperties.TryGetValue("iothub-connection-device-id", out object devId);
                                data.SystemProperties.TryGetValue("iothub-connection-module-id", out object modId);

                                if (devId != null && devId.ToString().Equals(deviceId) &&
                                    modId != null && modId.ToString().Equals(this.name))
                                {
                                    result.TrySetResult(true);
                                    return true;
                                }

                                return false;
                            }));

                    await result.Task;
                }

                await receiver.CloseAsync();
                await client.CloseAsync();
            }

            return Profiler.Run(
                $"Receiving events from device '{deviceId}' on Event Hub '{builder.EntityPath}'",
                _ReceiveEventsAsync
            );
        }
    }
}