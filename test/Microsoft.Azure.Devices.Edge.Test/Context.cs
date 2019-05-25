// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.IO;
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

                string GetStr(string name) => context.GetValue<string>(name);

                Option<(string, string, string)> AllOrNothing(string a, string b, string c) =>
                    string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || string.IsNullOrEmpty(c)
                        ? Option.None<(string, string, string)>()
                        : Option.Some((a, b, c));

                return new Context
                {
                    ConnectionString = GetStr("IOT_HUB_CONNECTION_STRING"),
                    EventHubEndpoint = GetStr("EVENT_HUB_ENDPOINT"),
                    DeviceId = context.GetValue("deviceId", $"end-to-end-{DateTime.Now:yyyy'-'MM'-'dd'T'HH'-'mm'-'ss'-'fff}"),
                    InstallerPath = Option.Maybe(GetStr("installerPath")),
                    PackagePath = Option.Maybe(GetStr("packagePath")),
                    Proxy = Option.Maybe(context.GetValue<Uri>("proxy")),
                    Registry = AllOrNothing(GetStr("registry"), GetStr("user"), GetStr("CONTAINER_REGISTRY_PASSWORD")),
                    EdgeAgent = Option.Maybe(GetStr("edgeAgent")),
                    EdgeHub = Option.Maybe(GetStr("edgeHub")),
                    TempSensor = Option.Maybe(GetStr("tempSensor")),
                    MethodSender = Option.Maybe(GetStr("methodSender")),
                    MethodReceiver = Option.Maybe(GetStr("methodReceiver")),
                    LogFile = Option.Maybe(GetStr("logFile")),
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

        public Option<string> EdgeAgent { get; private set; }

        public Option<string> EdgeHub { get; private set; }

        public Option<string> TempSensor { get; private set; }

        public Option<string> MethodSender { get; private set; }

        public Option<string> MethodReceiver { get; private set; }

        public Option<string> LogFile { get; private set; }

        public bool Verbose { get; private set; }

        public TimeSpan SetupTimeout { get; private set; }

        public TimeSpan TeardownTimeout { get; private set; }

        public TimeSpan TestTimeout { get; private set; }
    }
}
