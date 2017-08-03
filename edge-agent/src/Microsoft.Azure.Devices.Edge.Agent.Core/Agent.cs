// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Azure.Devices.Edge.Util;
	using Microsoft.Azure.Devices.Edge.Util.Concurrency;
	using Microsoft.Extensions.Logging;

	public class Agent
	{
		readonly IEnvironment environment;
		readonly IPlanner planner;
		readonly IConfigSource configSource;

		public Agent(IConfigSource configSource, IEnvironment environment, IPlanner planner)
		{
			this.configSource = Preconditions.CheckNotNull(configSource, nameof(configSource));
			this.environment = Preconditions.CheckNotNull(environment, nameof(environment));
			this.planner = Preconditions.CheckNotNull(planner, nameof(planner));
			Events.AgentCreated();
		}

		public async Task ReconcileAsync(CancellationToken token)
		{
			Task<ModuleSet> envTask = this.environment.GetModulesAsync(token);
			Task<ModuleSet> configTask = this.configSource.GetModuleSetAsync();

			await Task.WhenAll(envTask, configTask);

			ModuleSet current = envTask.Result;
			ModuleSet desired = configTask.Result;
			Plan plan = this.planner.Plan(desired, current);

			if (!plan.IsEmpty)
			{
				try
				{
					await plan.ExecuteAsync(token);
				}
				catch (Exception ex)
				{
					Events.PlanExecutionFailed(ex);
					throw;
				}
			}
		}

		static class Events
		{
			static readonly ILogger Log = Logger.Factory.CreateLogger<Agent>();
			const int IdStart = AgentEventIds.Agent;

			enum EventIds
			{
				AgentCreated = IdStart,
				UpdateDesiredStateFailed,
				PlanExecutionFailed
			}

			public static void AgentCreated()
			{
				Log.LogDebug((int)EventIds.AgentCreated, "Agent Created.");
			}

			public static void UpdateDesiredStateFailed()
			{
				Log.LogError((int)EventIds.UpdateDesiredStateFailed, "Agent update to desired state failed.");
			}

			public static void PlanExecutionFailed(Exception ex)
			{
				Log.LogError((int)EventIds.PlanExecutionFailed, ex, "Agent Plan execution failed.");
			}
		}

	}
}