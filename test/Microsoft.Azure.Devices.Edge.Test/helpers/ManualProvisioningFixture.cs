// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;
    using NestedEdgeConfig = Microsoft.Azure.Devices.Edge.Test.Common.EdgeDevice.NestedEdgeConfig;

    // NUnit's [Timeout] attribute isn't supported in .NET Standard
    // and even if it were, it doesn't run the teardown method when
    // a test times out. We need teardown to run, to remove the
    // device registration from IoT Hub and stop the daemon. So
    // we have our own timeout mechanism.
    public class ManualProvisioningFixture : BaseFixture
    {
        protected readonly IotHub iotHub;
        protected IEdgeDaemon daemon;
        protected CertificateAuthority ca;

        public ManualProvisioningFixture()
        {
            this.iotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.Proxy);
        }

        [OneTimeSetUp]
        protected async Task BeforeAllTestsAsync()
        {
            using var cts = new CancellationTokenSource(Context.Current.SetupTimeout);
            this.daemon = await OsPlatform.Current.CreateEdgeDaemonAsync(
                Context.Current.InstallerPath,
                cts.Token);
        }

        protected async Task ConfigureDaemonAsync(
            Func<DaemonConfiguration, Task<(string, object[])>> config,
            EdgeDevice device,
            DateTime startTime,
            CancellationToken token)
        {
            await this.daemon.ConfigureAsync(config, token);

            try
            {
                await this.daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                var agent = new EdgeAgent(device.Id, this.iotHub);
                await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                await agent.PingAsync(token);
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

        protected NestedEdgeConfig GetNestedEdgeConfig()
        {
            return new NestedEdgeConfig(Context.Current.NestedEdge,
                Context.Current.ParentDeviceId,
                Context.Current.Hostname);
        }

        public async Task SetUpCertificatesAsync(CancellationToken token, DateTime startTime, string deviceId)
        {
            (string, string, string) rootCa =
                Context.Current.RootCaKeys.Expect(() => new InvalidOperationException("Missing root CA keys"));
            string caCertScriptPath =
                Context.Current.CaCertScriptPath.Expect(() => new InvalidOperationException("Missing CA cert script path"));
            string certId = Context.Current.Hostname.GetOrElse(deviceId);

            try
            {
                this.ca = await CertificateAuthority.CreateAsync(
                    certId,
                    rootCa,
                    caCertScriptPath,
                    token);

                CaCertificates caCert = await this.ca.GenerateCaCertificatesAsync(certId, token);
                this.ca.EdgeCertificates = caCert;
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
