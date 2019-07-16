// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;

    public class DeviceBase : TestBase
    {
        IEdgeDaemon daemon;

        protected EdgeCertificateAuthority edgeCa;
        protected IotHub iotHub;

        [OneTimeSetUp]
        public async Task BeforeAllAsync()
        {
            await Profiler.Run(
                async () =>
                {
                    string agentImage = Context.Current.EdgeAgentImage.Expect(() => new ArgumentException());
                    string hubImage = Context.Current.EdgeHubImage.Expect(() => new ArgumentException());
                    (string caCertPath, string caKeyPath, string caPassword) =
                        Context.Current.RootCaKeys.Expect(() => new ArgumentException());
                    Option<Uri> proxy = Context.Current.Proxy;
                    string deviceId = Context.Current.DeviceId;

                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        CancellationToken token = cts.Token;

                        this.iotHub = new IotHub(
                            Context.Current.ConnectionString,
                            Context.Current.EventHubEndpoint,
                            proxy);

                        // TODO: RootCertificateAuthority only exists to create the EdgeCertificateAuthority; the functionality of the former can be folded into the latter
                        var rootCa = await RootCertificateAuthority.CreateAsync(
                            caCertPath,
                            caKeyPath,
                            caPassword,
                            Context.Current.CaCertScriptPath,
                            token);

                        this.edgeCa = await rootCa.CreateEdgeCertificateAuthorityAsync(deviceId, token);

                        this.daemon = Platform.CreateEdgeDaemon(Context.Current.InstallerPath);
                        await this.daemon.ConfigureAsync(
                            config =>
                            {
                                config.SetCertificates(this.edgeCa.Certificates);
                                config.Update();
                                return Task.FromResult(("with edge certificates", Array.Empty<object>()));
                            },
                            token);

                        var runtime = new Runtime(
                            deviceId,
                            agentImage,
                            hubImage,
                            proxy,
                            Context.Current.Registries,
                            Context.Current.OptimizeForPerformance,
                            iotHub);

                        await runtime.DeployConfigurationAsync(_ => { }, token);
                        await runtime.WaitForModulesRunningAsync(new EdgeModule[0], token);
                    }
                },
                "Completed custom certificate setup");
        }

        [OneTimeTearDown]
        public async Task AfterAllAsync()
        {
            await Profiler.Run(
                async () =>
                {
                    using (var cts = new CancellationTokenSource(Context.Current.TeardownTimeout))
                    {
                        await this.daemon.ConfigureAsync(
                            config =>
                            {
                                config.RemoveCertificates();
                                config.Update();
                                return Task.FromResult(("without edge certificates", Array.Empty<object>()));
                            },
                            cts.Token);
                    }
                },
                "Completed custom certificate teardown");
        }
    }
}
