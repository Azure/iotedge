// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.IO;
    using System.Net;
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

                Option<(string, string, string)> GetAndValidateRegistry()
                {
                    // If any container registry arguments (server, username, password)
                    // are given, then they must *all* be given, otherwise throw an error.
                    string registry = Get("registry");
                    string username = Get("user");
                    string password = Get("CONTAINER_REGISTRY_PASSWORD");

                    if (!string.IsNullOrWhiteSpace(registry) ||
                        !string.IsNullOrWhiteSpace(username) ||
                        !string.IsNullOrWhiteSpace(password))
                    {
                        Preconditions.CheckNonWhiteSpace(registry, nameof(registry));
                        Preconditions.CheckNonWhiteSpace(username, nameof(username));
                        Preconditions.CheckNonWhiteSpace(password, nameof(password));
                        return Option.Some((registry, username, password));
                    }

                    return Option.None<(string, string, string)>();
                }

                this.ConnectionString = Get("IOT_HUB_CONNECTION_STRING");
                this.EventHubEndpoint = Get("EVENT_HUB_ENDPOINT");
                this.DeviceId = GetOrDefault("deviceId", $"end-to-end-{Dns.GetHostName()}-{DateTime.Now:yyyy'-'MM'-'dd'T'HH'-'mm'-'ss'-'fff}");
                this.InstallerPath = Option.Maybe(Get("installerPath"));
                this.PackagePath = Option.Maybe(Get("packagePath"));
                this.Proxy = Option.Maybe(context.GetValue<Uri>("proxy"));
                this.Registry = GetAndValidateRegistry();
                this.EdgeAgentImage = Option.Maybe(Get("edgeAgentImage"));
                this.EdgeHubImage = Option.Maybe(Get("edgeHubImage"));
                this.TempSensorImage = Option.Maybe(Get("tempSensorImage"));
                this.MethodSenderImage = Option.Maybe(Get("methodSenderImage"));
                this.MethodReceiverImage = Option.Maybe(Get("methodReceiverImage"));
                this.LogFile = Option.Maybe(Get("logFile"));
                this.Verbose = context.GetValue<bool>("verbose");
                this.OptimizeForPerformance = context.GetValue("optimizeForPerformance", true);
                this.SetupTimeout = TimeSpan.FromMinutes(context.GetValue("setupTimeoutMinutes", 5));
                this.TeardownTimeout = TimeSpan.FromMinutes(context.GetValue("teardownTimeoutMinutes", 2));
                this.TestTimeout = TimeSpan.FromMinutes(context.GetValue("testTimeoutMinutes", 5));
        }

        static readonly Lazy<Context> Default = new Lazy<Context>(() => new Context());

        public static Context Current => Default.Value;

        public string ConnectionString { get; }

        public string EventHubEndpoint { get; }

        public string DeviceId { get; }

        public Option<string> InstallerPath { get; }

        public Option<string> PackagePath { get; }

        public Option<Uri> Proxy { get; }

        public Option<(string address, string username, string password)> Registry { get; }

        public Option<string> EdgeAgentImage { get; }

        public Option<string> EdgeHubImage { get; }

        public Option<string> TempSensorImage { get; }

        public Option<string> MethodSenderImage { get; }

        public Option<string> MethodReceiverImage { get; }

        public Option<string> LogFile { get; }

        public bool OptimizeForPerformance { get; }

        public bool Verbose { get; }

        public TimeSpan SetupTimeout { get; }

        public TimeSpan TeardownTimeout { get; }

        public TimeSpan TestTimeout { get; }
    }
}
