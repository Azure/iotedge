// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
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

        async Task<(ModuleSet current, Exception ex)> GetCurrentModuleSetAsync(CancellationToken token)
        {
            ModuleSet current = null;
            Exception ex = null;

            try
            {
                current = await this.environment.GetModulesAsync(token);
            }
            catch(Exception e)
            {
                ex = e;
            }

            return (current, ex);
        }

        async Task<(DeploymentConfigInfo deploymentConfigInfo, Exception ex)> GetDeploymentConfigInfoAsync()
        {
            DeploymentConfigInfo deploymentConfigInfo = null;
            Exception ex = null;

            try
            {
                deploymentConfigInfo = await this.configSource.GetDeploymentConfigInfoAsync();
            }
            catch(Exception e)
            {
                ex = e;
            }

            return (deploymentConfigInfo, ex);
        }

        async Task<(ModuleSet current, DeploymentConfigInfo DeploymentConfigInfo, Exception ex)> GetReconcileData(CancellationToken token)
        {
            // we read the data from the config source and from the environment separately because
            // when doing something like TaskEx.WhenAll(t1, t2) if either of them throws then we get
            // nothing; so for example, if the environment is able to successfully retrieve the moduleset
            // but there's a corrupt deployment in IoT Hub then we end up not being able to report the
            // current state even though we have it

            var ((current, environmentException), (deploymentConfigInfo, configSourceException)) = await TaskEx.WhenAll(
                this.GetCurrentModuleSetAsync(token), this.GetDeploymentConfigInfoAsync()
            );

            var exceptions = new Exception[]
            {
                environmentException,
                configSourceException,
                deploymentConfigInfo?.Exception.OrDefault()
            }.Where(e => e != null);

            Exception exception = null;
            if (exceptions.Count() > 1)
            {
                exception = new AggregateException(exceptions);
            }
            else if (exceptions.Count() > 0)
            {
                exception = exceptions.First();
            }

            return (current, deploymentConfigInfo, exception);
        }

        public async Task ReconcileAsync(CancellationToken token)
        {
            ModuleSet current, updated = null;
            DeploymentConfigInfo deploymentConfigInfo = null;

            try
            {
                Exception exception;
                (current, deploymentConfigInfo, exception) = await this.GetReconcileData(token);
                updated = current;
                if (exception != null)
                {
                    throw exception;
                }

                DeploymentConfig deploymentConfig = deploymentConfigInfo.DeploymentConfig;
                if (deploymentConfig != DeploymentConfig.Empty)
                {
                    ModuleSet desiredModuleSet = deploymentConfig.GetModuleSet();
                    IImmutableDictionary<string, IModuleIdentity> identities = await this.moduleIdentityLifecycleManager.GetModuleIdentitiesAsync(
                        desiredModuleSet, current
                    );
                    Plan plan = await this.planner.PlanAsync(desiredModuleSet, current, identities);
                    if (!plan.IsEmpty)
                    {
                        try
                        {
                            await plan.ExecuteAsync(token);

                            // get post plan execution state
                            updated = await this.environment.GetModulesAsync(token);
                        }
                        catch (Exception ex)
                        {
                            Events.PlanExecutionFailed(ex);

                            // even though plan execution failed, the environment might
                            // still have changed (as a result of partial execution of
                            // the plan for example)
                            updated = await this.environment.GetModulesAsync(token);
                            throw;
                        }
                    }
                }

                await this.reporter.ReportAsync(token, updated, deploymentConfigInfo, DeploymentStatus.Success);
            }
            catch(ConfigEmptyException ex)
            {
                var status = new DeploymentStatus(DeploymentStatusCode.ConfigEmptyError, ex.Message);
                await this.reporter.ReportAsync(token, updated, deploymentConfigInfo, status);
                Events.EmptyConfig(ex);
            }
            catch(InvalidSchemaVersionException ex)
            {
                var status = new DeploymentStatus(DeploymentStatusCode.InvalidSchemaVersion, ex.Message);
                await this.reporter.ReportAsync(token, updated, deploymentConfigInfo, status);
                Events.InvalidSchemaVersion(ex);
            }
            catch(ConfigFormatException ex)
            {
                var status = new DeploymentStatus(DeploymentStatusCode.ConfigFormatError, ex.Message);
                await this.reporter.ReportAsync(token, updated, deploymentConfigInfo, status);
                Events.InvalidConfigFormat(ex);
            }
            catch(Exception ex)
            {
                var status = new DeploymentStatus(DeploymentStatusCode.Failed, ex.Message);
                await this.reporter.ReportAsync(token, updated, deploymentConfigInfo, status);
                throw;
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<Agent>();
            const int IdStart = AgentEventIds.Agent;

            enum EventIds
            {
                AgentCreated = IdStart,
                PlanExecutionFailed,
                EmptyConfig,
                InvalidSchemaVersion,
                InvalidConfigFormat
            }

            public static void AgentCreated()
            {
                Log.LogDebug((int)EventIds.AgentCreated, "Agent Created.");
            }

            public static void PlanExecutionFailed(Exception ex)
            {
                Log.LogError((int)EventIds.PlanExecutionFailed, ex, "Agent Plan execution failed.");
            }

            public static void EmptyConfig(ConfigEmptyException ex)
            {
                Log.LogDebug((int)EventIds.EmptyConfig, ex.Message);
            }

            public static void InvalidSchemaVersion(InvalidSchemaVersionException ex)
            {
                Log.LogWarning((int)EventIds.InvalidSchemaVersion, ex.Message);
            }

            public static void InvalidConfigFormat(ConfigFormatException ex)
            {
                Log.LogWarning((int)EventIds.InvalidConfigFormat, ex.Message);
            }
        }
    }
}
