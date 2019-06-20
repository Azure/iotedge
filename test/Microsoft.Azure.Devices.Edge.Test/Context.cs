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
        static readonly Lazy<Context> Default = new Lazy<Context>(
            () =>
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

                return new Context
                {
                    ConnectionString = Get("IOT_HUB_CONNECTION_STRING"),
                    EventHubEndpoint = Get("EVENT_HUB_ENDPOINT"),
                    DeviceId = GetOrDefault("deviceId", $"end-to-end-{Dns.GetHostName()}-{DateTime.Now:yyyy'-'MM'-'dd'T'HH'-'mm'-'ss'-'fff}"),
                    InstallerPath = Option.Maybe(Get("installerPath")),
                    PackagePath = Option.Maybe(Get("packagePath")),
                    Proxy = Option.Maybe(context.GetValue<Uri>("proxy")),
                    Registry = GetAndValidateRegistry(),
                    EdgeAgentImage = Option.Maybe(Get("edgeAgentImage")),
                    EdgeHubImage = Option.Maybe(Get("edgeHubImage")),
                    TempSensorImage = Option.Maybe(Get("tempSensorImage")),
                    MethodSenderImage = Option.Maybe(Get("methodSenderImage")),
                    MethodReceiverImage = Option.Maybe(Get("methodReceiverImage")),
                    LogFile = Option.Maybe(Get("logFile")),
                    Verbose = context.GetValue("verbose", false),
                    SetupTimeout = TimeSpan.FromMinutes(context.GetValue("setupTimeoutMinutes", 5)),
                    TeardownTimeout = TimeSpan.FromMinutes(context.GetValue("teardownTimeoutMinutes", 2)),
                    TestTimeout = TimeSpan.FromMinutes(context.GetValue("testTimeoutMinutes", 5))
                };
            });

        public static Context Current => Default.Value;

        public string ConnectionString { get; private set; }

        public string EventHubEndpoint { get; private set; }

        public string DeviceId { get; private set; }

        public Option<string> InstallerPath { get; private set; }

        public Option<string> PackagePath { get; private set; }

        public Option<Uri> Proxy { get; private set; }

        public Option<(string address, string username, string password)> Registry { get; private set; }

        public Option<string> EdgeAgentImage { get; private set; }

        public Option<string> EdgeHubImage { get; private set; }

        public Option<string> TempSensorImage { get; private set; }

        public Option<string> MethodSenderImage { get; private set; }

        public Option<string> MethodReceiverImage { get; private set; }

        public Option<string> LogFile { get; private set; }

        public bool Verbose { get; private set; }

        public TimeSpan SetupTimeout { get; private set; }

        public TimeSpan TeardownTimeout { get; private set; }

        public TimeSpan TestTimeout { get; private set; }
    }
}
