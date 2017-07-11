// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Commands
{
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
		static readonly ILogger Logger = Util.Logger.Factory.CreateLogger<RemoveCommand>();
		readonly IDockerClient client;
		readonly DockerModule module;
		const string LogLinesToPull = "25";
		const int WaitForLogs = 30000; //ms

		public RemoveCommand(IDockerClient client, DockerModule module)
		{
			this.client = Preconditions.CheckNotNull(client, nameof(client));
			this.module = Preconditions.CheckNotNull(module, nameof(module));
		}

		async Task TailLogsAsync(DockerModule module)
		{
			using (var cts = new CancellationTokenSource())
			{
				var parameters = new ContainerLogsParameters
				{
					ShowStdout = true,
					Tail = RemoveCommand.LogLinesToPull
				};
				cts.CancelAfter(RemoveCommand.WaitForLogs);
				using (var stream = await this.client.Containers.GetContainerLogsAsync(module.Name, parameters, cts.Token))
				{
					bool firstLine = true;
					using (var reader = new StreamReader(stream))
					{
						while (stream.CanRead && !reader.EndOfStream)
						{
							if (firstLine)
							{
								Logger.LogInformation($"Last {RemoveCommand.LogLinesToPull} log lines from container {module.Name}:");
								firstLine = false;
							}
							var line = await reader.ReadLineAsync();
							Logger.LogInformation(line);
						}
					}
				}
			}
		}

		public async Task ExecuteAsync(CancellationToken token)
		{
			var parameters = new ContainerRemoveParameters();
			await this.TailLogsAsync(this.module);
			await this.client.Containers.RemoveContainerAsync(this.module.Name, parameters);
		}

		public Task UndoAsync(CancellationToken token) => TaskEx.Done;

		public string Show() => $"docker rm {this.module.Name}";
	}
}