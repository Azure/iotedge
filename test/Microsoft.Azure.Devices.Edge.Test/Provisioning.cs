// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;

    public class Provisioning : BaseFixture
    {
        protected readonly IEdgeDaemon daemon;
        protected readonly IotHub iotHub;

        public Provisioning()
        {
            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath);
            this.iotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.Proxy);
        }

        string DeriveDeviceKey(byte[] groupKey, string registrationId)
        {
            using (var hmac = new HMACSHA256(groupKey))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(registrationId)));
            }
        }

        [Test]
        public async Task DpsSymmetricKey()
        {
            await Profiler.Run(
                async () =>
                {
                    string scopeId = Context.Current.DpsScopeId.Expect(() => new ArgumentException());
                    string registrationId = Context.Current.DpsRegistrationId.Expect(() => new ArgumentException());
                    string groupKey = Context.Current.DpsGroupKey.Expect(() => new ArgumentException());
                    string deviceKey = this.DeriveDeviceKey(Convert.FromBase64String(groupKey), registrationId);

                    CancellationToken token = this.cts.Token;

                    await this.daemon.UninstallAsync(token);
                    await this.daemon.InstallAsync(
                        scopeId,
                        registrationId,
                        deviceKey,
                        Context.Current.PackagePath,
                        Context.Current.Proxy,
                        token);

                    await this.daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                    Option<EdgeDevice> device = Option.None<EdgeDevice>();
                    try
                    {
                        var agent = new EdgeAgent(registrationId, this.iotHub);
                        await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);

                        device = await EdgeDevice.GetIdentityAsync(
                            registrationId,
                            this.iotHub,
                            token);
                        device.Expect(() => new ArgumentException());

                        await agent.PingAsync(token);
                    }

                    // ReSharper disable once RedundantCatchClause
                    catch
                    {
                        throw;
                    }
                    finally
                    {
                        await this.daemon.StopAsync(token);
                        await device.ForEachAsync(dev => dev.MaybeDeleteIdentityAsync(token));
                    }
                },
                "Completed edge installation and provisioned with DPS");
        }
    }
}
