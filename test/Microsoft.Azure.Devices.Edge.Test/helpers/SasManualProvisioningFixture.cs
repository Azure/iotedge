// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;

    public class SasManualProvisioningFixture : ManualProvisioningFixture
    {
        protected EdgeRuntime runtime;

        public SasManualProvisioningFixture()
            : base()
        {
        }

        public SasManualProvisioningFixture(string connectionString, string eventHubEndpoint)
            : base(connectionString, eventHubEndpoint)
        {
        }

        [SetUp]
        public virtual async Task SasProvisionEdgeAsync()
        {
            using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
            {
                CancellationToken token = cts.Token;
                DateTime startTime = DateTime.Now;

                EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                    DeviceId.Current.Generate(),
                    this.iotHub,
                    AuthenticationType.Sas,
                    null,
                    token);

                Context.Current.DeleteList.TryAdd(device.Id, device);

                this.runtime = new EdgeRuntime(
                    device.Id,
                    Context.Current.EdgeAgentImage,
                    Context.Current.EdgeHubImage,
                    Context.Current.Proxy,
                    Context.Current.Registries,
                    Context.Current.OptimizeForPerformance,
                    this.iotHub);

                (string hubHostname, string deviceId, string key) = this.ParseConnectionString(device.ConnectionString);

                await this.ConfigureDaemonAsync(
                    config =>
                    {
                        config.SetManualSasProvisioning(hubHostname, deviceId, key);
                        config.Update();
                        return Task.FromResult((
                            "with connection string for device '{Identity}'",
                            new object[] { device.Id }));
                    },
                    device,
                    startTime,
                    token);
            }
        }

        (string, string, string) ParseConnectionString(string connectionString)
        {
            const string HOST_NAME = "HostName";
            const string DEVICE_ID = "DeviceId";
            const string ACCESS_KEY = "SharedAccessKey";

            Dictionary<string, string> parts = new Dictionary<string, string>()
            {
                { HOST_NAME, string.Empty },
                { DEVICE_ID, string.Empty },
                { ACCESS_KEY, string.Empty }
            };

            string[] parameters = connectionString.Split(";");

            foreach (string p in parameters)
            {
                string[] parameter = p.Split("=", 2);

                if (parts.ContainsKey(parameter[0]))
                {
                    parts[parameter[0]] = parameter[1];
                }
                else
                {
                    throw new System.InvalidOperationException($"Bad connection string {connectionString}");
                }
            }

            foreach (KeyValuePair<string, string> i in parts)
            {
                if (string.IsNullOrEmpty(i.Value))
                {
                    throw new System.InvalidOperationException($"Bad connection string {connectionString}");
                }
            }

            return (parts[HOST_NAME], parts[DEVICE_ID], parts[ACCESS_KEY]);
        }
    }
}
