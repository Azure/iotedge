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

                return new Context(
                    context.GetValue<string>("IOT_HUB_CONNECTION_STRING"),
                    context.GetValue<string>("EVENT_HUB_ENDPOINT"),
                    context.GetValue("deviceId", string.Empty),
                    context.GetValue("installerPath", string.Empty),
                    context.GetValue("packagePath", string.Empty),
                    context.GetValue("proxy", string.Empty),
                    context.GetValue("registry", string.Empty),
                    context.GetValue("user", string.Empty),
                    context.GetValue("CONTAINER_REGISTRY_PASSWORD", string.Empty),
                    context.GetValue("edgeAgent", string.Empty),
                    context.GetValue("edgeHub", string.Empty),
                    context.GetValue("tempSensor", string.Empty),
                    context.GetValue("methodSender", string.Empty),
                    context.GetValue("methodReceiver", string.Empty),
                    context.GetValue("logFile", string.Empty),
                    context.GetValue("verbose", false),
                    context.GetValue("timeoutMinutes", 5));
            });

        public Context(string connectionString, string eventHubEndpoint, string deviceId, string installerPath, string packagePath, string proxy, string registry, string user, string password, string edgeAgent, string edgeHub, string tempSensor, string methodSender, string methodReceiver, string logFile, bool verbose, int timeout)
        {
            string CreateDeviceId()
            {
                return $"end-to-end-{DateTime.Now:yyyy'-'MM'-'dd'T'HH'-'mm'-'ss'-'fff}";
            }

            this.ConnectionString = connectionString;
            this.EventHubEndpoint = eventHubEndpoint;
            this.DeviceId = string.IsNullOrEmpty(deviceId) ? CreateDeviceId() : deviceId;
            this.InstallerPath = string.IsNullOrEmpty(installerPath) ? Option.None<string>() : Option.Some(installerPath);
            this.PackagePath = string.IsNullOrEmpty(packagePath) ? Option.None<string>() : Option.Some(packagePath);
            this.Proxy = string.IsNullOrEmpty(proxy) ? Option.None<Uri>() : Option.Some(new Uri(proxy));
            this.Registry = string.IsNullOrEmpty(registry) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password) ? Option.None<(string, string, string)>() : Option.Some((registry, user, password));
            this.EdgeAgent = string.IsNullOrEmpty(edgeAgent) ? Option.None<string>() : Option.Some(edgeAgent);
            this.EdgeHub = string.IsNullOrEmpty(edgeHub) ? Option.None<string>() : Option.Some(edgeHub);
            this.TempSensor = string.IsNullOrEmpty(tempSensor) ? Option.None<string>() : Option.Some(tempSensor);
            this.MethodSender = string.IsNullOrEmpty(methodSender) ? Option.None<string>() : Option.Some(methodSender);
            this.MethodReceiver = string.IsNullOrEmpty(methodReceiver) ? Option.None<string>() : Option.Some(methodReceiver);
            this.LogFile = string.IsNullOrEmpty(logFile) ? Option.None<string>() : Option.Some(logFile);
            this.Verbose = verbose;
            this.Timeout = TimeSpan.FromMinutes(timeout);
        }

        public static Context Current => Default.Value;

        public string ConnectionString { get; }
        public string EventHubEndpoint { get; }
        public string DeviceId { get; }
        public Option<string> InstallerPath { get; }
        public Option<string> PackagePath { get; }
        public Option<Uri> Proxy { get; }
        public Option<(string address, string username, string password)> Registry;
        public Option<string> EdgeAgent { get; }
        public Option<string> EdgeHub { get; }
        public Option<string> TempSensor { get; }
        public Option<string> MethodSender { get; }
        public Option<string> MethodReceiver { get; }
        public Option<string> LogFile { get; }
        public bool Verbose { get; }
        public TimeSpan Timeout { get; }
    }
}
