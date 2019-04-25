// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.TempSensor
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Test
    {
        public class Args
        {
            public string DeviceId;
            public string ConnectionString;
            public string Endpoint;
            public string InstallerPath;
            public Option<string> PackagesPath;
            public Option<Uri> Proxy;
            public string AgentImage;
            public string HubImage;
            public string SensorImage;
            public Option<(string address, string username, string password)> Registry;
        }

        public Task<int> RunAsync(Args args) => Profiler.Run(
            "Running tempSensor test",
            async () =>
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                {
                    CancellationToken token = cts.Token;

                    // ** setup
                    var iotHub = new IotHub(args.ConnectionString, args.Endpoint, args.Proxy);
                    var device = await EdgeDevice.GetOrCreateIdentityAsync(
                        args.DeviceId,
                        iotHub,
                        token);

                    var daemon = new EdgeDaemon(args.InstallerPath);
                    await daemon.UninstallAsync(token);
                    await daemon.InstallAsync(
                        device.ConnectionString,
                        args.PackagesPath,
                        args.Proxy,
                        token);

                    await args.Proxy.Match(
                        async p =>
                        {
                            await daemon.StopAsync(token);
                            var yaml = new DaemonConfiguration();
                            yaml.AddHttpsProxy(p);
                            yaml.Update();
                            await daemon.StartAsync(token);
                        },
                        () => daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token)
                    );

                    var agent = new EdgeAgent(device.Id, iotHub);
                    await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                    await agent.PingAsync(token);

                    // ** test
                    var config = new EdgeConfiguration(device.Id, args.AgentImage, iotHub);
                    args.Registry.ForEach(
                        r => config.AddRegistryCredentials(r.address, r.username, r.password)
                    );
                    config.AddEdgeHub(args.HubImage);
                    args.Proxy.ForEach(p => config.AddProxy(p));
                    config.AddTempSensor(args.SensorImage);
                    await config.DeployAsync(token);

                    var hub = new EdgeModule("edgeHub", device.Id, iotHub);
                    var sensor = new EdgeModule("tempSensor", device.Id, iotHub);
                    await EdgeModule.WaitForStatusAsync(
                        new[] { hub, sensor },
                        EdgeModuleStatus.Running,
                        token);
                    await sensor.WaitForEventsReceivedAsync(token);

                    var sensorTwin = new ModuleTwin(sensor.Id, device.Id, iotHub);
                    await sensorTwin.UpdateDesiredPropertiesAsync(
                        new
                        {
                            properties = new
                            {
                                desired = new
                                {
                                    SendData = true,
                                    SendInterval = 10
                                }
                            }
                        },
                        token);
                    await sensorTwin.WaitForReportedPropertyUpdatesAsync(
                        new
                        {
                            properties = new
                            {
                                reported = new
                                {
                                    SendData = true,
                                    SendInterval = 10
                                }
                            }
                        },
                        token);

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
