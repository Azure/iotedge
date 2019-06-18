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

                string GetString(string name) => context.GetValue<string>(name);

                string GetStringOr(string name, string alternate)
                {
                    string value = GetString(name);
                    return string.IsNullOrEmpty(value) ? alternate : value;
                }

                Option<(string, string, string)> AllOrNothing(string a, string b, string c) =>
                    string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || string.IsNullOrEmpty(c)
                        ? Option.None<(string, string, string)>()
                        : Option.Some((a, b, c));

                return new Context
                {
                    ConnectionString = GetString("IOT_HUB_CONNECTION_STRING"),
                    EventHubEndpoint = GetString("EVENT_HUB_ENDPOINT"),
                    DeviceId = GetStringOr("deviceId", $"end-to-end-{Dns.GetHostName()}-{DateTime.Now:yyyy'-'MM'-'dd'T'HH'-'mm'-'ss'-'fff}"),
                    InstallerPath = Option.Maybe(GetString("installerPath")),
                    PackagePath = Option.Maybe(GetString("packagePath")),
                    Proxy = Option.Maybe(context.GetValue<Uri>("proxy")),
                    Registry = AllOrNothing(GetString("registry"), GetString("user"), GetString("CONTAINER_REGISTRY_PASSWORD")),
                    EdgeAgentImage = Option.Maybe(GetString("edgeAgentImage")),
                    EdgeHubImage = Option.Maybe(GetString("edgeHubImage")),
                    TempSensorImage = Option.Maybe(GetString("tempSensorImage")),
                    MethodSenderImage = Option.Maybe(GetString("methodSenderImage")),
                    MethodReceiverImage = Option.Maybe(GetString("methodReceiverImage")),
                    LogFile = Option.Maybe(GetString("logFile")),
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
