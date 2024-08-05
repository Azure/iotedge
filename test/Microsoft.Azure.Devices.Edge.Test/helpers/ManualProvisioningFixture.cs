// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using NestedEdgeConfig = Microsoft.Azure.Devices.Edge.Test.Common.EdgeDevice.NestedEdgeConfig;

    // NUnit's [Timeout] attribute isn't supported in .NET Standard
    // and even if it were, it doesn't run the teardown method when
    // a test times out. We need teardown to run, to remove the
    // device registration from IoT Hub and stop the daemon. So
    // we have our own timeout mechanism.
    public class ManualProvisioningFixture : BaseFixture
    {
        public static IotHub IotHub { get; } = new IotHub(
            Context.Current.ConnectionString,
            Context.Current.EventHubEndpoint,
            Context.Current.TestRunnerProxy);

        protected static IEdgeDaemon daemon;
        protected static CertificateAuthority ca;
        protected static TestContext msTestContext;

        [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
        public static async Task BeforeAllTestsAsync(TestContext msTestContextObj)
        {
            msTestContext = msTestContextObj;
            using var cts = new CancellationTokenSource(Context.Current.SetupTimeout);
            daemon = await OsPlatform.Current.CreateEdgeDaemonAsync(Context.Current.PackagePath, cts.Token);
        }

        protected static async Task ConfigureDaemonAsync(
            Func<DaemonConfiguration, Task<(string, object[])>> config,
            EdgeDevice device,
            DateTime startTime,
            CancellationToken token)
        {
            await daemon.ConfigureAsync(config, token);

            try
            {
                var agent = new EdgeAgent(device.Id, IotHub);
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
                using var cts = new CancellationTokenSource(Context.Current.TeardownTimeout);
                await NUnitLogs.CollectAsync(startTime, msTestContext, cts.Token);
            }
        }

        protected static NestedEdgeConfig GetNestedEdgeConfig(IotHub iotHub)
        {
            return new NestedEdgeConfig(
                iotHub,
                Context.Current.NestedEdge,
                Context.Current.ParentDeviceId,
                Context.Current.ParentHostname,
                Context.Current.Hostname);
        }
    }
}
