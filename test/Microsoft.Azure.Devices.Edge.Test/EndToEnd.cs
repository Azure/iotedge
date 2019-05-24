// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;
    using Serilog;

    public class EndToEnd
    {
        [Test]
        public async Task TempSensor()
        {
            string name = "temp sensor";
            string edgeAgent = Context.Current.EdgeAgent.Expect(() => new ArgumentException());
            string edgeHub = Context.Current.EdgeHub.Expect(() => new ArgumentException());
            string tempSensor = Context.Current.TempSensor.Expect(() => new ArgumentException());

            try
            {
                using (var cts = new CancellationTokenSource(Context.Current.Timeout))
                {
                    Log.Information("Running test '{Name}'", name);
                    await Profiler.Run(
                        async () =>
                        {
                            CancellationToken token = cts.Token;

                            var iotHub = new IotHub(
                                Context.Current.ConnectionString,
                                Context.Current.EventHubEndpoint,
                                Context.Current.Proxy);

                            EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                                Context.Current.DeviceId,
                                iotHub,
                                token);

                            var config = new EdgeConfiguration(device.Id, edgeAgent, iotHub);
                            Context.Current.Registry.ForEach(
                                r => config.AddRegistryCredentials(r.address, r.username, r.password));
                            config.AddEdgeHub(edgeHub);
                            Context.Current.Proxy.ForEach(p => config.AddProxy(p));
                            config.AddModule("tempSensor", tempSensor);
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
                        },
                        "Completed test '{Name}'",
                        name);
                }
            }
            catch (OperationCanceledException e)
            {
                Log.Error(e, "Cancelled test '{Name}' after {Timeout} minutes", name, Context.Current.Timeout.TotalMinutes);
                throw;
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed test '{Name}'", name);
                throw;
            }
            finally
            {
                //Log.CloseAndFlush();
            }
        }
    }
}
