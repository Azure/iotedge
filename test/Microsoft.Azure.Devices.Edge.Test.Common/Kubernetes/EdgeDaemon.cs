// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Kubernetes
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Net;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public class EdgeDaemon : IEdgeDaemon
    {
        const string OverrideFile = "override.yaml";
        const string DefaultHelmRepo = " https://edgek8s.blob.core.windows.net/staging";

        public async Task InstallAsync(Option<string> packagesPath, Option<Uri> proxy, CancellationToken token)
        {
            // Create kind cluster,
            // create the device namespace
            // Helm set up repo location?
            // Run Helm install for CRD.
            string kindArgs = "create cluster --wait 10m";
            string kubectlArgs = $"create ns {Constants.Deployment}";
            string helmArgs = $"install {Constants.CrdDeployment} --repo {DefaultHelmRepo} edge-kubernetes-crd";

            var properties = new object[] { Dns.GetHostName() };
            string message = "Installed cluster and namespace on '{Device}'";

            Console.WriteLine($"COMMAND: kind {kindArgs}");
            Console.WriteLine($"COMMAND: kubectl {kubectlArgs}");
            Console.WriteLine($"COMMAND: helm {helmArgs}");

            await Profiler.Run(
                async () =>
                {
                    string[] output = await Process.RunAsync("kind", kindArgs, token);
                    Log.Verbose(string.Join("\n", output));
                    output = await Process.RunAsync("kubectl", kubectlArgs, token);
                    Log.Verbose(string.Join("\n", output));
                    output = await Process.RunAsync("helm", helmArgs, token);
                    Log.Verbose(string.Join("\n", output));
                },
                message,
                properties);
        }

        public Task ConfigureAsync(Func<IDaemonConfiguration, Task<(string, object[])>> config, CancellationToken token, bool restart)
        {
            // set up charts and launch config.
            // (Do we want to make Helm charts as artifacts? - eventually, yes)
            // Using a fixed namespace.
            // This is where a lot of the translation from config.yaml to
            // an override.yaml is going to happen.
            // Where do I save the override.yaml?
            // Am I "allowed" to have state in this class?
            //
            // Removed the internal "stop" and "start" here. should reconsider this.
            var properties = new object[] { };
            var message = "Configured edge daemon";

            return Profiler.Run(
                async () =>
                {
                    await this.InternalStopAsync(token);

                    var yaml = new DaemonConfiguration(OverrideFile);
                    (string msg, object[] props) = await config(yaml);

                    message += $" {msg}";
                    properties = properties.Concat(props).ToArray();

                    string[] output;
                    foreach (var k8sCmd in yaml.GetK8sCommands())
                    {
                        Console.WriteLine($"COMMAND: {k8sCmd.Item1} {k8sCmd.Item2}");

                        output = await Process.RunAsync(k8sCmd.Item1, k8sCmd.Item2, token);
                        Log.Verbose(string.Join("\n", output));
                    }

                    if (restart)
                    {
                        await this.InternalStartAsync(token);
                    }
                },
                message.ToString(),
                properties);
        }

        // Start and stop don't have a lot of meaning for edge on k8s.
        // Maybe this is where helm install is called?
        public Task StartAsync(CancellationToken token) => Profiler.Run(
            () => this.InternalStartAsync(token),
            "Started edge daemon");

        async Task InternalStartAsync(CancellationToken token)
        {
            string helmArgs = $"install -n {Constants.Deployment} {Constants.Deployment} --repo {DefaultHelmRepo} edge-kubernetes -f {OverrideFile}";
            Console.WriteLine($"COMMAND: helm {helmArgs}");

            string[] output = await Process.RunAsync("helm", helmArgs, token);
            Log.Verbose(string.Join("\n", output));
            await WaitForStatusAsync(ServiceControllerStatus.Running, token);
        }

        // same deal as start - maybe this is where Helm delete is done?
        public Task StopAsync(CancellationToken token) => Profiler.Run(
            () => this.InternalStopAsync(token),
            "Stopped edge daemon");

        async Task InternalStopAsync(CancellationToken token)
        {
            string listArgs = $"list -n {Constants.Deployment} -q";
            string[] helmList = await Process.RunAsync("helm", listArgs, token);

            Console.WriteLine(string.Join("\n", helmList));
            if (helmList.FirstOrDefault() == Constants.Deployment)
            {
                string helmArgs = $"delete -n {Constants.Deployment} {Constants.Deployment}";
                Console.WriteLine($"COMMAND: helm {helmArgs}");

                string[] output = await Process.RunAsync("helm", helmArgs, token);
                Console.WriteLine(string.Join("\n", output));
                await WaitForStatusAsync(ServiceControllerStatus.Stopped, token);
            }
        }

        public async Task UninstallAsync(CancellationToken token)
        {
            try
            {
                await this.InternalStopAsync(token);
            }
            catch (Win32Exception e)
            {
                Log.Verbose(e, "Failed to stop edge daemon, probably because it is already stopped");
            }

            try
            {
                await Profiler.Run(
                    async () =>
                    {
                        // This nukes the whole cluster. I don't think there's anything more to do.
                        string kindArgs = "delete cluster";
                        Console.WriteLine($"COMMAND: kind {kindArgs}");

                        string[] output = await Process.RunAsync("kind", kindArgs, token);
                        Console.WriteLine(string.Join("\n", output));
                    },
                    "Uninstalled edge daemon");
            }
            catch (Win32Exception e)
            {
                Log.Verbose(e, "Failed to uninstall edge daemon, probably because it isn't installed");
            }
        }

        public Task WaitForStatusAsync(EdgeDaemonStatus desired, CancellationToken token) => Profiler.Run(
            () => WaitForStatusAsync((ServiceControllerStatus)desired, token),
            "Edge daemon entered the '{Desired}' state",
            desired.ToString().ToLower());

        static async Task WaitForStatusAsync(ServiceControllerStatus desired, CancellationToken token)
        {
            while (true)
            {
                Func<string, bool> stateMatchesDesired;
                switch (desired)
                {
                    case ServiceControllerStatus.Running:
                        stateMatchesDesired = s => s == "Running";
                        break;
                    case ServiceControllerStatus.Stopped:
                        stateMatchesDesired = s => s != "Running";
                        break;
                    default:
                        throw new NotImplementedException($"No handler for {desired.ToString()}");
                }

                var edgeletPodOption = await KubeUtils.FindPod("iotedged", token);
                string currentState = await edgeletPodOption.Match(
                    async podName =>
                    {
                        string getPods = $"get pod --namespace {Constants.Deployment} {podName} --template=\"{{println .status.phase}}\"";

                        string[] output = await Process.RunAsync("kubectl", getPods, token);
                        Console.WriteLine(output.First());
                        return output.First();
                    },
                    () => Task.FromResult("NotFound"));

                if (stateMatchesDesired(currentState))
                {
                    break;
                }

                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }
    }
}
