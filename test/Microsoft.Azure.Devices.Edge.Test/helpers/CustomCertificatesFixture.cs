// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using NUnit.Framework;

    public class CustomCertificatesFixture : ManualProvisioningFixture
    {
        protected CertificateAuthority ca;

        [OneTimeSetUp]
        public async Task SetUpCertificatesAsync()
        {
            await Profiler.Run(
                async () =>
                {
                    (string, string, string) rootCa =
                        Context.Current.RootCaKeys.Expect(() => new InvalidOperationException("Missing root CA keys"));

                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        DateTime startTime = DateTime.Now;
                        CancellationToken token = cts.Token;

                        try
                        {
                            this.ca = await CertificateAuthority.CreateAsync(
                                Context.Current.DeviceId,
                                rootCa,
                                Context.Current.CaCertScriptPath,
                                token);

                            await this.daemon.ConfigureAsync(
                                config =>
                                {
                                    config.SetCertificates(this.ca.Certificates);
                                    config.Update();
                                    return Task.FromResult(("with edge certificates", Array.Empty<object>()));
                                },
                                token);

                            var runtime = new EdgeRuntime(
                                Context.Current.DeviceId,
                                Context.Current.EdgeAgentImage,
                                Context.Current.EdgeHubImage,
                                Context.Current.Proxy,
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
        public async Task RemoveCertificatesAsync()
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
