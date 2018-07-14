// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunners
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class OrderedRetryPlanRunner : IPlanRunner
    {
        readonly AsyncLock sync;
        long lastDeploymentId;
        Dictionary<string, CommandRunStats> commandRunStatus;
        readonly int maxRunCount;
        readonly int coolOffTimeUnitInSeconds;
        readonly ISystemTime systemTime;

        public OrderedRetryPlanRunner(int maxRunCount, int coolOffTimeUnitInSeconds, ISystemTime systemTime)
        {
            this.maxRunCount = Preconditions.CheckRange(
                maxRunCount, 1, nameof(maxRunCount)
            );
            this.coolOffTimeUnitInSeconds = Preconditions.CheckRange(
                coolOffTimeUnitInSeconds, 0, nameof(coolOffTimeUnitInSeconds)
            );
            this.systemTime = Preconditions.CheckNotNull(systemTime, nameof(systemTime));
            this.sync = new AsyncLock();
            this.lastDeploymentId = -1;
            this.commandRunStatus = new Dictionary<string, CommandRunStats>();
        }

        public async Task<bool> ExecuteAsync(long deploymentId, Plan plan, CancellationToken token)
        {
            Preconditions.CheckRange(deploymentId, -1, nameof(deploymentId));
            Preconditions.CheckNotNull(plan, nameof(plan));

            using (await this.sync.LockAsync())
            {
                Events.PlanExecStarted(deploymentId);

                // if this is a new deployment we haven't seen before then clear the
                // saved command run status values
                if (this.lastDeploymentId != -1 && this.lastDeploymentId != deploymentId)
                {
                    Events.NewDeployment(deploymentId);
                    this.commandRunStatus.Clear();
                }
                else
                {
                    if (this.lastDeploymentId != -1)
                    {
                        Events.OldDeployment(deploymentId);
                    }
                    else
                    {
                        Events.NewDeployment(deploymentId);
                    }
                }

                // update saved deployment ID
                this.lastDeploymentId = deploymentId;

                Option<List<Exception>> failures = Option.None<List<Exception>>();
                bool skippedModules = false;
                foreach (ICommand command in plan.Commands)
                {
                    var (shouldRun, runCount, coolOffPeriod, elapsedTime) = this.ShouldRunCommand(command);
                    try
                    {
                        if (token.IsCancellationRequested)
                        {
                            Events.PlanExecCancelled(deploymentId);
                            break;
                        }

                        if (shouldRun)
                        {
                            await command.ExecuteAsync(token);

                            // since this command ran successfully reset its
                            // run status
                            if (this.commandRunStatus.ContainsKey(command.Id))
                            {
                                this.commandRunStatus[command.Id] = CommandRunStats.Default;
                            }
                        }
                        else
                        {
                            skippedModules = true;
                            Events.SkippingCommand(deploymentId, command, this.commandRunStatus[command.Id], this.maxRunCount, coolOffPeriod, elapsedTime);
                        }
                    }
                    catch (Exception ex) when (ex.IsFatal() == false)
                    {
                        Events.PlanExecStepFailed(deploymentId, command, coolOffPeriod, elapsedTime);
                        if (!failures.HasValue)
                        {
                            failures = Option.Some(new List<Exception>());
                        }
                        failures.ForEach(f => f.Add(ex));

                        // since this command failed, record its status
                        int newRunCount = this.commandRunStatus.ContainsKey(command.Id) ?
                            this.commandRunStatus[command.Id].RunCount : 0;
                        this.commandRunStatus[command.Id] = new CommandRunStats(newRunCount + 1, this.systemTime.UtcNow, ex);
                    }
                }

                Events.PlanExecEnded(deploymentId);
                failures.ForEach(f => throw new AggregateException(f));
                return !skippedModules;
            }
        }

        private
        (
            bool shouldRun,
            int runCount,
            TimeSpan coolOffPeriod,
            TimeSpan elapsedTime
        ) ShouldRunCommand(ICommand command)
        {
            // the command should be run if there's no entry for it in our status dictionary
            if (this.commandRunStatus.ContainsKey(command.Id) == false)
            {
                return (true, -1, TimeSpan.MinValue, TimeSpan.MinValue);
            }

            CommandRunStats commandRunStatus = this.commandRunStatus[command.Id];

            // if this command has been run maxRunCount times already then don't
            // run it anymore
            if (commandRunStatus.RunCount == this.maxRunCount)
            {
                return (false, commandRunStatus.RunCount, TimeSpan.MinValue, TimeSpan.MinValue);
            }

            TimeSpan coolOffPeriod = TimeSpan.FromSeconds(
                this.coolOffTimeUnitInSeconds * Math.Pow(2, commandRunStatus.RunCount)
            );
            TimeSpan elapsedTime = this.systemTime.UtcNow - commandRunStatus.LastRunTimeUtc;

            return (elapsedTime > coolOffPeriod, commandRunStatus.RunCount, coolOffPeriod, elapsedTime);
        }

        class CommandRunStats
        {
            public int RunCount { get; }
            public DateTime LastRunTimeUtc { get; }
            public Option<Exception> Exception { get; }
            public bool LoggedWarning { get; set; }

            public static readonly CommandRunStats Default = new CommandRunStats(0, DateTime.MinValue);

            public CommandRunStats(int runCount, DateTime lastRunTimeUtc, Exception exception = null)
            {
                this.RunCount = runCount;
                this.LastRunTimeUtc = lastRunTimeUtc;
                this.Exception = Option.Maybe(exception);
                this.LoggedWarning = false;
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<OrderedRetryPlanRunner>();
            const int IdStart = AgentEventIds.OrderedRetryPlanRunner;

            enum EventIds
            {
                PlanExecStarted = IdStart,
                NewDeployment,
                OldDeployment,
                SkippingCommand,
                PlanExecStepFailed,
                PlanExecEnded,
                Cancelled,
            }

            public static void PlanExecStarted(long deploymentId) =>
                Log.LogInformation((int)EventIds.PlanExecStarted, $"Plan execution started for deployment {deploymentId}");

            public static void PlanExecCancelled(long deploymentId) =>
                Log.LogInformation((int)EventIds.Cancelled, $"Plan execution for deployment {deploymentId} was cancelled");

            public static void NewDeployment(long deploymentId) =>
                Log.LogDebug((int)EventIds.NewDeployment, $"Received new deployment {deploymentId}");

            public static void OldDeployment(long deploymentId) =>
                Log.LogDebug((int)EventIds.OldDeployment, $"Running plan on existing deployment {deploymentId}");

            public static void SkippingCommand(long deploymentId, ICommand command, CommandRunStats runStats, int maxRunCount, TimeSpan coolOffPeriod, TimeSpan elapsedTime)
            {
                string msg = $"Skipping command \"{command.Show()}\" in deployment {deploymentId}.";
                runStats.Exception.ForEach(ex => msg += $" Error: {ex.Message}.");

                if (runStats.RunCount == maxRunCount)
                {
                    if (runStats.LoggedWarning == false)
                    {
                        msg += $" Command has been tried {runStats.RunCount} times. Giving up (max run count is {maxRunCount}).";
                        Log.LogWarning((int)EventIds.SkippingCommand, msg);
                        runStats.LoggedWarning = true;
                    }
                }
                else
                {
                    msg += $" Will retry in {(coolOffPeriod - elapsedTime).Humanize()}";
                    Log.LogDebug((int)EventIds.SkippingCommand, msg);
                }
            }

            public static void PlanExecStepFailed(long deploymentId, ICommand command, TimeSpan coolOffPeriod, TimeSpan elapsedTime) =>
                Log.LogError(
                    (int)EventIds.PlanExecStepFailed,
                    $"Step failed in deployment {deploymentId}, continuing execution. Failure when running command {command.Show()}. Will retry in {(coolOffPeriod - elapsedTime).Humanize()}.");

            public static void PlanExecEnded(long deploymentId)
            {
                Log.LogInformation((int)EventIds.PlanExecEnded, $"Plan execution ended for deployment {deploymentId}");
            }
        }
    }
}
