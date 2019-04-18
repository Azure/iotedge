// Copyright (c) Microsoft. All rights reserved.

namespace temp_sensor
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using common;

    class Program
    {
        // args[0] - device ID
        // args[1] - path to IotEdgeSecurityDaemon.ps1
        // args[2] - container registry address
        // args[3] - container registry username
        static Task<int> Main(string[] args)
        {
            return Profiler.Run(
                "Running tempSensor test",
                async () => {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                    {
                        CancellationToken token = cts.Token;

                        // ** setup
                        string iotHubConnectionString =
                            EnvironmentVariable.Expect("E2E_IOT_HUB_CONNECTION_STRING");
                        string eventHubEndpoint =
                            EnvironmentVariable.Expect("E2E_EVENT_HUB_ENDPOINT");
                        string registryPassword =
                            EnvironmentVariable.Expect("E2E_CONTAINER_REGISTRY_PASSWORD");

                        var iotHub = new IotHub(iotHubConnectionString, eventHubEndpoint);
                        var device = await EdgeDevice.GetOrCreateIdentityAsync(
                            args[0], iotHub, token);

                        var daemon = new EdgeDaemon(args[1]);
                        await daemon.UninstallAsync(token);
                        await daemon.InstallAsync(device.ConnectionString, token);
                        await daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                        var agent = new EdgeAgent(device.Id, iotHub);
                        await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                        await agent.PingAsync(token);

                        // ** test
                        var config = new EdgeConfiguration(device.Id, iotHub);
                        config.AddRegistryCredentials(args[2], args[3], registryPassword);
                        config.AddEdgeHub();
                        config.AddTempSensor();
                        await config.DeployAsync(token);

                        var hub = new EdgeModule("edgeHub", device.Id, iotHub);
                        var sensor = new EdgeModule("tempSensor", device.Id, iotHub);
                        await EdgeModule.WaitForStatusAsync(
                            new[] { hub, sensor }, EdgeModuleStatus.Running, token);
                        await sensor.WaitForEventsReceivedAsync(token);

                        var sensorTwin = new ModuleTwin(sensor.Id, device.Id, iotHub);
                        await sensorTwin.UpdateDesiredPropertiesAsync(new
                        {
                            properties = new
                            {
                                desired = new
                                {
                                    SendData = true,
                                    SendInterval = 10
                                }
                            }
                        }, token);
                        await sensorTwin.WaitForReportedPropertyUpdatesAsync(new
                        {
                            properties = new
                            {
                                reported = new
                                {
                                    SendData = true,
                                    SendInterval = 10
                                }
                            }
                        }, token);

                        // ** teardown
                        await daemon.StopAsync(token);
                        await device.MaybeDeleteIdentityAsync(token);
                    }

                    return 0;
                },
                "Completed tempSensor test"
            );
        }
    }
}
