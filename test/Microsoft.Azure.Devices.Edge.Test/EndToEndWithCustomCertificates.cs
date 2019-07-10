// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;
    using Serilog;

    class EndToEndWithCustomCertificates
    {
        CancellationTokenSource cts;
        IEdgeDaemon daemon;
        EdgeCertificateAuthority edgeCa;
        IotHub iotHub;
        DateTime testStartTime;

        [OneTimeSetUp]
        public async Task BeforeAll()
        {
            await Profiler.Run(
                async () =>
                {
                    string agentImage = Context.Current.EdgeAgentImage.Expect(() => new ArgumentException());
                    string hubImage = Context.Current.EdgeHubImage.Expect(() => new ArgumentException());
                    (string caCertPath, string caKeyPath, string caPassword) =
                        Context.Current.RootCaKeys.Expect(() => new ArgumentException());
                    bool optimizeForPerformance = Context.Current.OptimizeForPerformance;
                    Option<Uri> proxy = Context.Current.Proxy;

                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        CancellationToken token = cts.Token;

                        this.iotHub = new IotHub(
                            Context.Current.ConnectionString,
                            Context.Current.EventHubEndpoint,
                            proxy);

                        EdgeDevice device = (await EdgeDevice.GetIdentityAsync(
                            Context.Current.DeviceId,
                            this.iotHub,
                            token)).Expect(() => new Exception("Device should have already been created in setup fixture"));

                        // TODO: RootCertificateAuthority only exists to create the EdgeCertificateAuthority; the functionality of the former can be folded into the latter
                        var rootCa = await RootCertificateAuthority.CreateAsync(
                            caCertPath,
                            caKeyPath,
                            caPassword,
                            Context.Current.CaCertScriptPath,
                            token);

                        this.edgeCa = await rootCa.CreateEdgeCertificateAuthorityAsync(Context.Current.DeviceId, token);

                        this.daemon = Platform.CreateEdgeDaemon(Context.Current.InstallerPath);
                        await this.daemon.ConfigureAsync(
                            config =>
                            {
                                config.SetCertificates(this.edgeCa.Certificates);
                                config.Update();
                                return Task.FromResult(("with edge certificates", Array.Empty<object>()));
                            },
                            token);

                        var builder = new EdgeConfigBuilder(device.Id);
                        foreach ((string address, string username, string password) in Context.Current.Registries)
                        {
                            builder.AddRegistryCredentials(address, username, password);
                        }
                        builder.AddEdgeAgent(agentImage).WithProxy(proxy);
                        builder.AddEdgeHub(hubImage, optimizeForPerformance).WithProxy(proxy);
                        await builder.Build().DeployAsync(this.iotHub, token);

                        var hub = new EdgeModule("edgeHub", device.Id, this.iotHub);
                        await hub.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                    }
                },
                "Completed custom certificate setup");
        }

        [OneTimeTearDown]
        public async Task AfterAll()
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

        [SetUp]
        public void BeforeAny()
        {
            this.cts = new CancellationTokenSource(Context.Current.TestTimeout);
            this.testStartTime = DateTime.Now;
        }

        [TearDown]
        public async Task AfterAnyAsync()
        {
            await Profiler.Run(
                async () =>
                {
                    this.cts.Dispose();

                    if (TestContext.CurrentContext.Result.Outcome != ResultState.Ignored)
                    {
                        using (var cts = new CancellationTokenSource(Context.Current.TeardownTimeout))
                        {
                            string prefix = $"{Context.Current.DeviceId}-{TestContext.CurrentContext.Test.NormalizedName()}";
                            IEnumerable<string> paths = await EdgeLogs.CollectAsync(this.testStartTime, prefix, cts.Token);
                            foreach (string path in paths)
                            {
                                TestContext.AddTestAttachment(path);
                            }
                        }
                    }
                },
                "Completed test teardown");
        }

        static readonly (AuthenticationType, Protocol, bool)[] TransparentGatewayArgs =
        {
            (AuthenticationType.Sas, Protocol.Mqtt, false),
            (AuthenticationType.Sas, Protocol.Amqp, false),
            (AuthenticationType.Sas, Protocol.Mqtt, true),
            (AuthenticationType.Sas, Protocol.Amqp, true),
            (AuthenticationType.CertificateAuthority, Protocol.Mqtt, true),
            // (AuthenticationType.CertificateAuthority, Protocol.Amqp, true), // TODO: Failing in recent builds, uncomment when fixed
            (AuthenticationType.SelfSigned, Protocol.Mqtt, true),
            // (AuthenticationType.SelfSigned, Protocol.Amqp, true) // TODO: Failing in recent builds, uncomment when fixed
        };

        [TestCaseSource(nameof(TransparentGatewayArgs))]
        public async Task TransparentGateway((AuthenticationType, Protocol, bool) args)
        {
            (AuthenticationType auth, Protocol protocol, bool inScope) = args;

            CancellationToken token = this.cts.Token;

            string name = $"transparent gateway ({auth.ToString()}, {protocol.ToString()}, inScope={inScope})";
            Log.Information("Running test '{Name}'", name);

            await Profiler.Run(
                async () =>
                {
                    string suffix = inScope ? "scoped-leaf" : "leaf";
                    string leafDeviceId = $"{Context.Current.DeviceId}-{protocol.ToString()}-{auth.ToString()}-{suffix}";
                    Option<string> parentId = inScope ? Option.Some(Context.Current.DeviceId) : Option.None<string>();

                    var leaf = await LeafDevice.CreateAsync(leafDeviceId, protocol, auth, parentId, this.edgeCa, this.iotHub, token);

                    try
                    {
                        await leaf.SendEventAsync(token);
                        await leaf.WaitForEventsReceivedAsync(token);
                        await leaf.InvokeDirectMethodAsync(token);
                    }

                    // According to C# reference docs for 'try-finally', the finally
                    // block may or may not run for unhandled exceptions. The
                    // workaround is to catch the exception here and rethrow,
                    // guaranteeing that the finally block will run.
                    // ReSharper disable once RedundantCatchClause
                    catch
                    {
                        throw;
                    }
                    finally
                    {
                        await leaf.DeleteIdentityAsync(token);
                    }
                },
                "Completed test '{Name}'",
                name);
        }
    }
}
