// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;
    using Serilog;

    public class Provisioning : DeviceProvisioningFixture
    {
        protected readonly IotHub iotHub;

        public Provisioning()
        {
            this.iotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.Proxy);
        }

        private string DeriveDeviceKey(byte[] groupKey, string registrationId)
        {
            using (var hmac = new HMACSHA256(groupKey))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(registrationId)));
            }
        }

        private string GetRegistrationId()
        {
            return $"{Context.Current.DeviceId}-{TestContext.CurrentContext.Test.NormalizedName()}";
        }

        [Test]
        public async Task DpsSymmetricKey()
        {
            string idScope = Context.Current.DpsIdScope.Expect(() => new InvalidOperationException("Missing DPS ID scope"));
            string groupKey = Context.Current.DpsGroupKey.Expect(() => new InvalidOperationException("Missing DPS enrollment group key"));
            string registrationId = this.GetRegistrationId();

            string deviceKey = this.DeriveDeviceKey(Convert.FromBase64String(groupKey), registrationId);

            CancellationToken token = this.TestToken;

            await this.daemon.ConfigureAsync(
                config =>
                {
                    config.SetDpsSymmetricKey(idScope, registrationId, deviceKey);
                    config.Update();
                    return Task.FromResult((
                        "with DPS symmetric key attestation for '{Identity}'",
                        new object[] { registrationId }));
                },
                token);

            await this.daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

            var agent = new EdgeAgent(registrationId, this.iotHub);
            await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
            await agent.PingAsync(token);

            Option<EdgeDevice> device = await EdgeDevice.GetIdentityAsync(
                registrationId,
                this.iotHub,
                token,
                takeOwnership: true);
            Context.Current.DeleteList.TryAdd(registrationId, device.Expect(() => new InvalidOperationException(
                $"Device '{registrationId}' should have been created by DPS, but was not found in '{this.iotHub.Hostname}'")));
        }

        [Test]
        public async Task DpsX509()
        {
            (string, string, string) rootCa =
                        Context.Current.RootCaKeys.Expect(() => new InvalidOperationException("Missing root CA keys"));
            string caCertScriptPath =
                        Context.Current.CaCertScriptPath.Expect(() => new InvalidOperationException("Missing CA cert script path"));
            string idScope = Context.Current.DpsIdScope.Expect(() => new InvalidOperationException("Missing DPS ID scope"));
            string registrationId = this.GetRegistrationId();

            Assert.IsTrue(File.Exists(Path.Combine(caCertScriptPath,"certGen.sh")), "CA Certification script does not exist. Path: " + $"{caCertScriptPath}" );

            CertificateAuthority ca;
            CancellationToken token = this.TestToken;

            using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
            {
                DateTime startTime = DateTime.Now;
                CancellationToken caToken = cts.Token;

                ca = await CertificateAuthority.CreateDpsX509CaAsync(
                    registrationId,
                    rootCa,
                    caCertScriptPath,
                    caToken);

                await this.daemon.ConfigureAsync(
                    config =>
                    {
                        config.SetDpsX509(idScope, registrationId, ca);
                        config.Update();
                        return Task.FromResult((
                            "with DPS X509 attestation for '{Identity}'",
                            new object[] { registrationId }));
                    },
                    caToken);
            }

            await this.daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

            var agent = new EdgeAgent(registrationId, this.iotHub);
            await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
            Log.Information("Device ID: " + registrationId);

            await agent.PingAsync(token);

            Option<EdgeDevice> device = await EdgeDevice.GetIdentityAsync(
                registrationId,
                this.iotHub,
                token,
                takeOwnership: true);

            Context.Current.DeleteList.TryAdd(
                registrationId,
                device.Expect(() => new InvalidOperationException(
                    $"Device '{registrationId}' should have been created by DPS, but was not found in '{this.iotHub.Hostname}'")));
        }
    }
}
