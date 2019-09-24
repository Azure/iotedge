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

            string GetOrDefault(string name, string defaultValue) => context.GetValue(name, defaultValue);

            IEnumerable<(string, string, string)> GetAndValidateRegistries()
            {
                var result = new List<(string, string, string)>();

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
                    Preconditions.CheckNonWhiteSpace(address, nameof(address));
                    Preconditions.CheckNonWhiteSpace(username, nameof(username));
                    Preconditions.CheckNonWhiteSpace(password, nameof(password));

                    result.Add((address, username, password));
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
                    Preconditions.CheckNonWhiteSpace(certificate, nameof(certificate));
                    Preconditions.CheckNonWhiteSpace(key, nameof(key));
                    Preconditions.CheckNonWhiteSpace(password, nameof(password));
                    Preconditions.CheckArgument(File.Exists(certificate));
                    Preconditions.CheckArgument(File.Exists(key));
                    return Option.Some((certificate, key, password));
                }

                return Option.None<(string, string, string)>();
            }

            string defaultId =
                $"e2e-{string.Concat(Dns.GetHostName().Take(14)).TrimEnd(new[] { '-' })}-{DateTime.Now:yyMMdd'-'HHmmss'.'fff}";

            this.CaCertScriptPath = Option.Maybe(Get("caCertScriptPath"));
            this.ConnectionString = Get("IOT_HUB_CONNECTION_STRING");
            this.DeviceId = IdentityLimits.CheckEdgeId(GetOrDefault("deviceId", defaultId));
            this.DpsIdScope = Option.Maybe(Get("dpsIdScope"));
            this.DpsGroupKey = Option.Maybe(Get("DPS_GROUP_KEY"));
            this.EdgeAgentImage = Option.Maybe(Get("edgeAgentImage"));
            this.EdgeHubImage = Option.Maybe(Get("edgeHubImage"));
            this.EventHubEndpoint = Get("EVENT_HUB_ENDPOINT");
            this.InstallerPath = Option.Maybe(Get("installerPath"));
            this.LogFile = Option.Maybe(Get("logFile"));
            this.MethodReceiverImage = Option.Maybe(Get("methodReceiverImage"));
            this.MethodSenderImage = Option.Maybe(Get("methodSenderImage"));
            this.OptimizeForPerformance = context.GetValue("optimizeForPerformance", true);
            this.PackagePath = Option.Maybe(Get("packagePath"));
            this.Proxy = Option.Maybe(context.GetValue<Uri>("proxy"));
            this.Registries = GetAndValidateRegistries();
            this.RootCaKeys = GetAndValidateRootCaKeys();
            this.SetupTimeout = TimeSpan.FromMinutes(context.GetValue("setupTimeoutMinutes", 5));
            this.TeardownTimeout = TimeSpan.FromMinutes(context.GetValue("teardownTimeoutMinutes", 2));
            this.TempFilterFuncImage = Option.Maybe(Get("tempFilterFuncImage"));
            this.TempFilterImage = Option.Maybe(Get("tempFilterImage"));
            this.TempSensorImage = Option.Maybe(Get("tempSensorImage"));
            this.TestTimeout = TimeSpan.FromMinutes(context.GetValue("testTimeoutMinutes", 5));
            this.Verbose = context.GetValue<bool>("verbose");
        }

        static readonly Lazy<Context> Default = new Lazy<Context>(() => new Context());

        public static Context Current => Default.Value;

        public Option<string> CaCertScriptPath { get; }

        public string ConnectionString { get; }

        public Dictionary<string, EdgeDevice> DeleteList { get; } = new Dictionary<string, EdgeDevice>();

        public string DeviceId { get; }

        public Option<string> DpsIdScope { get; }

        public Option<string> DpsGroupKey { get; }

        public Option<string> EdgeAgentImage { get; }

        public Option<string> EdgeHubImage { get; }

        public string EventHubEndpoint { get; }

        public Option<string> InstallerPath { get; }

        public Option<string> LogFile { get; }

        public Option<string> MethodReceiverImage { get; }

        public Option<string> MethodSenderImage { get; }

        public bool OptimizeForPerformance { get; }

        public Option<string> PackagePath { get; }

        public Option<Uri> Proxy { get; }

        public IEnumerable<(string address, string username, string password)> Registries { get; }

        public Option<(string certificate, string key, string password)> RootCaKeys { get; }

        public TimeSpan SetupTimeout { get; }

        public TimeSpan TeardownTimeout { get; }

        public Option<string> TempFilterFuncImage { get; }

        public Option<string> TempFilterImage { get; }

        public Option<string> TempSensorImage { get; }

        public TimeSpan TestTimeout { get; }

        public bool Verbose { get; }
    }
}
