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

    public class CustomCertificatesFixture : BaseFixture
    {
        IEdgeDaemon daemon;

        protected CertificateAuthority ca;
        protected IotHub iotHub;

        [OneTimeSetUp]
        public async Task BeforeAllAsync()
        {
            await Profiler.Run(
                async () =>
                {
                    (string, string, string) rootCa =
                        Context.Current.RootCaKeys.Expect(() => new ArgumentException());
                    Option<Uri> proxy = Context.Current.Proxy;
                    string deviceId = Context.Current.DeviceId;

                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        DateTime startTime = DateTime.Now;
                        CancellationToken token = cts.Token;

                        this.iotHub = new IotHub(
                            Context.Current.ConnectionString,
                            Context.Current.EventHubEndpoint,
                            proxy);

                        try
                        {
                            this.ca = await CertificateAuthority.CreateAsync(
                                deviceId,
                                rootCa,
                                Context.Current.CaCertScriptPath,
                                token);

                            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath);
                            await this.daemon.ConfigureAsync(
                                config =>
                                {
                                    config.SetCertificates(this.ca.Certificates);
                                    config.Update();
                                    return Task.FromResult(("with edge certificates", Array.Empty<object>()));
                                },
                                token);

                            var runtime = new EdgeRuntime(
                                deviceId,
                                Context.Current.EdgeAgentImage,
                                Context.Current.EdgeHubImage,
                                proxy,
                                Context.Current.Registries,
                                Context.Current.OptimizeForPerformance,
                                this.iotHub);

                            await runtime.DeployConfigurationAsync(token);
                        }

                        // ReSharper disable once RedundantCatchClause
                        catch
                        {
                            throw;
                        }
                        finally
                        {
                            await NUnitLogs.CollectAsync(startTime, token);
                        }
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
