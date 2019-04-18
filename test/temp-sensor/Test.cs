// Copyright (c) Microsoft. All rights reserved.

namespace temp_sensor
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using common;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Test
    {
        string deviceId;
        string connectionString;
        string endpoint;
        string installerPath;
        string agentImage;
        string hubImage;
        string sensorImage;
        Option<(string address, string username, string password)> registry;

        public Test(
            string deviceId,
            string connectionString,
            string endpoint,
            string installerPath,
            string agentImage,
            string hubImage,
            string sensorImage,
            Option<(string address, string username, string password)> registry
        )
        {
            this.deviceId = deviceId;
            this.connectionString = connectionString;
            this.endpoint = endpoint;
            this.installerPath = installerPath;
            this.agentImage = agentImage;
            this.hubImage = hubImage;
            this.sensorImage = sensorImage;
            this.registry = registry;
        }

        public Task<int> RunAsync()
        {
            return Profiler.Run(
                "Running tempSensor test",
                async () =>
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                    {
                        CancellationToken token = cts.Token;

                        // ** setup
                        var iotHub = new IotHub(connectionString, endpoint);
                        var device = await EdgeDevice.GetOrCreateIdentityAsync(
                            this.deviceId, iotHub, token);

                        var daemon = new EdgeDaemon(this.installerPath);
                        await daemon.UninstallAsync(token);
                        await daemon.InstallAsync(device.ConnectionString, token);
                        await daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                        var agent = new EdgeAgent(device.Id, iotHub);
                        await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                        await agent.PingAsync(token);

                        // ** test
                        var config = new EdgeConfiguration(device.Id, this.agentImage, iotHub);
                        this.registry.ForEach(
                            r => config.AddRegistryCredentials(r.address, r.username, r.password)
                        );
                        config.AddEdgeHub(this.hubImage);
                        config.AddTempSensor(this.sensorImage);
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