// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class Agent
    {
        readonly IEnvironment environment;
        readonly IPlanner planner;
        readonly IReporter reporter;
        readonly IConfigSource configSource;
        readonly IModuleIdentityLifecycleManager moduleIdentityLifecycleManager;

        public Agent(IConfigSource configSource, IEnvironment environment, IPlanner planner, IReporter reporter, IModuleIdentityLifecycleManager moduleIdentityLifecycleManager)
        {
            this.configSource = Preconditions.CheckNotNull(configSource, nameof(configSource));
            this.environment = Preconditions.CheckNotNull(environment, nameof(environment));
            this.planner = Preconditions.CheckNotNull(planner, nameof(planner));
            this.reporter = Preconditions.CheckNotNull(reporter, nameof(reporter));
            this.moduleIdentityLifecycleManager = Preconditions.CheckNotNull(moduleIdentityLifecycleManager, nameof(moduleIdentityLifecycleManager));
            Events.AgentCreated();
        }

        public async Task ReconcileAsync(CancellationToken token)
        {
            var (current, deploymentConfigInfo) = await TaskEx.WhenAll(
                this.environment.GetModulesAsync(token),
                this.configSource.GetDeploymentConfigInfoAsync()
            );
            ModuleSet updated = current;
            DeploymentConfig deploymentConfig = deploymentConfigInfo.DeploymentConfig;
            if (deploymentConfig != DeploymentConfig.Empty)
            {
                ModuleSet desiredModuleSet = deploymentConfig.GetModuleSet();
                IImmutableDictionary<string, IModuleIdentity> identities = await this.moduleIdentityLifecycleManager.GetModuleIdentities(desiredModuleSet, current);
                Plan plan = await this.planner.PlanAsync(deploymentConfig.GetModuleSet(), current, identities);
                if (!plan.IsEmpty)
                {
                    try
                    {
                        await plan.ExecuteAsync(token);
                        updated = await this.environment.GetModulesAsync(token);
                    }
                    catch (Exception ex)
                    {
                        Events.PlanExecutionFailed(ex);

                        updated = await this.environment.GetModulesAsync(token);
                        await this.reporter.ReportAsync(token, updated, deploymentConfigInfo, new DeploymentStatus(DeploymentStatusCode.Failed, ex.Message));

                        throw;
                    }
                }
            }

            await this.reporter.ReportAsync(token, updated, deploymentConfigInfo, DeploymentStatus.Success);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<Agent>();
            const int IdStart = AgentEventIds.Agent;

            enum EventIds
            {
                AgentCreated = IdStart,
                PlanExecutionFailed
            }

            public static void AgentCreated()
            {
                Log.LogDebug((int)EventIds.AgentCreated, "Agent Created.");
            }

            public static void PlanExecutionFailed(Exception ex)
            {
                Log.LogError((int)EventIds.PlanExecutionFailed, ex, "Agent Plan execution failed.");
            }
        }

    }
}
