// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public class Context
    {
        public Context()
        {
            IConfiguration context = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("context.json")
                .AddEnvironmentVariables("E2E_")
                .Build();

            string Get(string name) => context.GetValue<string>(name);

            IEnumerable<Registry> GetAndValidateRegistries()
            {
                var result = new List<Registry>();

                var registries = context.GetSection("registries").GetChildren().ToArray();
                foreach (var reg in registries)
                {
                    string address = reg.GetValue<string>("address");
                    string username = reg.GetValue<string>("username");
                    // To specify a password as an environment variable instead of in the
                    // JSON file (so it's not stored in the clear on the filesystem), name
                    // the variable like this: E2E_REGISTRIES__<index>__PASSWORD, where
                    // <index> is the 0-based number corresponding to an element in the
                    // "registries" JSON array.
                    string password = reg.GetValue<string>("PASSWORD");

                    // If any container registry arguments (server, username, password)
                    // are given, then they must *all* be given, otherwise throw an error.
                    Preconditions.CheckArgument(!string.IsNullOrWhiteSpace(address), $"Container registry address is missing from context.json.");
                    Preconditions.CheckArgument(!string.IsNullOrWhiteSpace(username), $"Container registry username is missing from context.json.");
                    Preconditions.CheckArgument(
                        !string.IsNullOrWhiteSpace(password),
                        $"Container registry password is missing. Please set E2E_REGISTRIES__0__PASSWORD " +
                        "env var (preferable) or place it in context.json");

                    result.Add(new Registry(address, username, password));
                }

                return result;
            }

            Option<(string, string, string)> GetAndValidateRootCaKeys()
            {
                // If any root CA key materials (cert file, key file, password) are
                // given, then they must *all* be given, otherwise throw an error.
                string certificate = Get("rootCaCertificatePath");
                string key = Get("rootCaPrivateKeyPath");
                string password = Get("ROOT_CA_PASSWORD");

                if (!string.IsNullOrWhiteSpace(certificate) ||
                    !string.IsNullOrWhiteSpace(key) ||
                    !string.IsNullOrWhiteSpace(password))
                {
                    Preconditions.CheckArgument(!string.IsNullOrWhiteSpace(certificate), $"rootCaCertificatePath is missing from context.json.");
                    Preconditions.CheckArgument(!string.IsNullOrWhiteSpace(key), $"rootCaPrivateKeyPath is missing from context.json.");
                    Preconditions.CheckArgument(!string.IsNullOrWhiteSpace(password), $"ROOT_CA_PASSWORD is missing from environment or context.json.");
                    Preconditions.CheckArgument(File.Exists(certificate), "rootCaCertificatePath file does not exist");
                    Preconditions.CheckArgument(File.Exists(key), "rootCaPrivateKeyPath file does not exist");
                    return Option.Some((certificate, key, password));
                }

                return Option.None<(string, string, string)>();
            }

            string defaultId =
                $"e2e-{string.Concat(Dns.GetHostName().Take(14)).TrimEnd(new[] { '-' })}-{DateTime.Now:yyMMdd'-'HHmmss'.'fff}";

            this.CaCertScriptPath = Option.Maybe(Get("caCertScriptPath"));
            this.ConnectionString = Get("IOT_HUB_CONNECTION_STRING");
            Preconditions.CheckArgument(!string.IsNullOrWhiteSpace(this.ConnectionString), $"IOT_HUB_CONNECTION_STRING is missing from environment or context.json.");
            this.ParentDeviceId = Option.Maybe(Get("parentDeviceId"));
            this.DpsIdScope = Option.Maybe(Get("dpsIdScope"));
            this.DpsGroupKey = Option.Maybe(Get("DPS_GROUP_KEY"));
            this.EdgeAgentImage = Option.Maybe(Get("edgeAgentImage"));
            this.EdgeHubImage = Option.Maybe(Get("edgeHubImage"));
            this.DiagnosticsImage = Option.Maybe(Get("diagnosticsImage"));
            this.EventHubEndpoint = Get("EVENT_HUB_ENDPOINT");
            Preconditions.CheckArgument(!string.IsNullOrWhiteSpace(this.EventHubEndpoint), $"EVENT_HUB_ENDPOINT is missing from environment or context.json.");
            this.InstallerPath = Option.Maybe(Get("installerPath"));
            this.LogFile = Option.Maybe(Get("logFile"));
            this.MethodReceiverImage = Option.Maybe(Get("methodReceiverImage"));
            this.MethodSenderImage = Option.Maybe(Get("methodSenderImage"));
            this.OptimizeForPerformance = context.GetValue("optimizeForPerformance", true);
            this.PackagePath = Option.Maybe(Get("packagePath"));
            this.TestRunnerProxy = Option.Maybe(context.GetValue<Uri>("testRunnerProxy"));
            this.EdgeProxy = Option.Maybe(context.GetValue<Uri>("edgeProxy"));
            this.Registries = GetAndValidateRegistries();
            this.RootCaKeys = GetAndValidateRootCaKeys();
            this.SetupTimeout = TimeSpan.FromMinutes(context.GetValue("setupTimeoutMinutes", 5));
            this.TeardownTimeout = TimeSpan.FromMinutes(context.GetValue("teardownTimeoutMinutes", 2));
            this.TempFilterFuncImage = Option.Maybe(Get("tempFilterFuncImage"));
            this.TempFilterImage = Option.Maybe(Get("tempFilterImage"));
            this.TempSensorImage = Option.Maybe(Get("tempSensorImage"));
            this.NumberLoggerImage = Option.Maybe(Get("numberLoggerImage"));
            this.MetricsValidatorImage = Option.Maybe(Get("metricsValidatorImage"));
            this.TestResultCoordinatorImage = Option.Maybe(Get("testResultCoordinatorImage"));
            this.LoadGenImage = Option.Maybe(Get("loadGenImage"));
            this.RelayerImage = Option.Maybe(Get("relayerImage"));
            this.GenericMqttTesterImage = Option.Maybe(Get("genericMqttTesterImage"));
            this.NetworkControllerImage = Option.Maybe(Get("networkControllerImage"));
            this.TestTimeout = TimeSpan.FromMinutes(context.GetValue("testTimeoutMinutes", 5));
            this.Verbose = context.GetValue<bool>("verbose");
            this.ParentHostname = Option.Maybe(Get("parentHostname"));
            this.Hostname = Option.Maybe(Get("hostname"));
            this.BlobSasUrl = Option.Maybe(Get("BLOB_STORE_SAS"));
            this.NestedEdge = context.GetValue("nestededge", false);
            this.DeviceId = Option.Maybe(Get("deviceId"));
            this.ISA95Tag = context.GetValue("isa95Tag", false);
            this.EnableManifestSigning = context.GetValue("enableManifestSigning", false);
            this.ManifestSigningDeploymentPath = Option.Maybe(Get("manifestSigningDeploymentPath"));
            this.ManifestSigningSignedDeploymentPath = Option.Maybe(Get("manifestSigningSignedDeploymentPath"));
            this.ManifestSigningGoodRootCaPath = Option.Maybe(Get("manifestSigningGoodRootCaPath"));
            this.ManifestSigningBadRootCaPath = Option.Maybe(Get("manifestSigningBadRootCaPath"));
            this.ManifestSigningDefaultLaunchSettings = Option.Maybe(Get("manifestSigningDefaultLaunchSettings"));
            this.ManifestSigningLaunchSettingsPath = Option.Maybe(Get("manifestSigningLaunchSettingsPath"));
            this.ManifestSignerClientBinPath = Option.Maybe(Get("manifestSignerClientBinPath"));
        }

        static readonly Lazy<Context> Default = new Lazy<Context>(() => new Context());

        public static Context Current => Default.Value;

        public Option<string> CaCertScriptPath { get; }

        public string ConnectionString { get; }

        public Option<string> ParentDeviceId { get; }

        public Dictionary<string, EdgeDevice> DeleteList { get; } = new Dictionary<string, EdgeDevice>();

        public Option<string> DpsIdScope { get; }

        public Option<string> DpsGroupKey { get; }

        public Option<string> EdgeAgentImage { get; }

        public Option<string> EdgeHubImage { get; }

        public Option<string> DiagnosticsImage { get; }

        public string EventHubEndpoint { get; }

        public Option<string> InstallerPath { get; }

        public Option<string> LogFile { get; }

        public Option<string> MethodReceiverImage { get; }

        public Option<string> MethodSenderImage { get; }

        public bool OptimizeForPerformance { get; }

        public Option<string> PackagePath { get; }

        public Option<Uri> TestRunnerProxy { get; }

        public Option<Uri> EdgeProxy { get; }

        public IEnumerable<Registry> Registries { get; }

        public Option<(string certificate, string key, string password)> RootCaKeys { get; }

        public TimeSpan SetupTimeout { get; }

        public TimeSpan TeardownTimeout { get; }

        public Option<string> TempFilterFuncImage { get; }

        public Option<string> TempFilterImage { get; }

        public Option<string> TempSensorImage { get; }

        public Option<string> NumberLoggerImage { get; }

        public Option<string> MetricsValidatorImage { get; }

        public Option<string> TestResultCoordinatorImage { get; }

        public Option<string> LoadGenImage { get; }

        public Option<string> RelayerImage { get; }

        public Option<string> GenericMqttTesterImage { get; }

        public Option<string> NetworkControllerImage { get; }

        public TimeSpan TestTimeout { get; }

        public bool Verbose { get; }

        public Option<string> ParentHostname { get; }

        public Option<string> DeviceId { get; }

        public Option<string> Hostname { get; }

        public Option<string> BlobSasUrl { get; }

        public bool NestedEdge { get; }

        public bool ISA95Tag { get; }

        public bool EnableManifestSigning { get; }

        public Option<string> ManifestSigningDeploymentPath { get; }

        public Option<string> ManifestSigningSignedDeploymentPath { get; }

        public Option<string> ManifestSigningGoodRootCaPath { get; }

        public Option<string> ManifestSigningBadRootCaPath { get; }

        public Option<string> ManifestSigningDefaultLaunchSettings { get; }

        public Option<string> ManifestSigningLaunchSettingsPath { get; }

        public Option<string> ManifestSignerClientBinPath { get; }
    }
}
