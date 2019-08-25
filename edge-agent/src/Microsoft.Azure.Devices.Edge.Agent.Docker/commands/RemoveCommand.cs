// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Commands
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class RemoveCommand : ICommand
    {
        const string LogLinesToPull = "25";
        const int WaitForLogs = 30000; // ms
        static readonly ILogger Logger = Util.Logger.Factory.CreateLogger<RemoveCommand>();
        readonly IDockerClient client;
        readonly DockerModule module;

        public RemoveCommand(IDockerClient client, DockerModule module)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.module = Preconditions.CheckNotNull(module, nameof(module));
        }

        public string Id => $"RemoveCommand({this.module.Name})";

        public async Task ExecuteAsync(CancellationToken token)
        {
            var parameters = new ContainerRemoveParameters();
            long exitCode = await this.GetModuleExitCode();
            if (exitCode != 0)
            {
                await this.TailLogsAsync(exitCode);
            }

            await this.client.Containers.RemoveContainerAsync(this.module.Name, parameters, token);
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"docker rm {this.module.Name}";

        async Task<long> GetModuleExitCode()
        {
            ContainerInspectResponse containerInfo = await this.client.Containers.InspectContainerAsync(this.module.Name);
            return containerInfo.State.ExitCode;
        }

        async Task TailLogsAsync(long exitCode)
        {
            Logger.LogError($"Module {this.module.Name} exited with a non-zero exit code of {exitCode}.");

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    var parameters = new ContainerLogsParameters
                    {
                        ShowStdout = true,
                        Tail = LogLinesToPull
                    };
                    cts.CancelAfter(WaitForLogs);
                    using (Stream stream = await this.client.Containers.GetContainerLogsAsync(this.module.Name, parameters, cts.Token))
                    {
                        bool firstLine = true;
                        using (var reader = new StreamReader(stream))
                        {
                            while (stream.CanRead && !reader.EndOfStream)
                            {
                                if (firstLine)
                                {
                                    Logger.LogError($"Last {LogLinesToPull} log lines from container {this.module.Name}:");
                                    firstLine = false;
                                }

                                string line = await reader.ReadLineAsync();
                                Logger.LogError(line);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unable to get logs from module {this.module.Name} - {ex.Message}");
            }
        }
    }
}
