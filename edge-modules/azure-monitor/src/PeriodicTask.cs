// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class PeriodicTask : IDisposable
    {
        private readonly Func<CancellationToken, Task> work;
        private readonly TimeSpan period;
        private readonly TimeSpan startAfter;
        private readonly object stateLock = new object();
        private readonly ILogger logger;
        private readonly string operationName;
        private readonly Timer checkTimer;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly bool verbose;
        private readonly bool instantStart;  // run the task immediately after starting, otherwise wait period before running for the first time

        private Task currentTask;

        public PeriodicTask(
            Func<CancellationToken, Task> work,
            TimeSpan period,
            TimeSpan startAfter,
            ILogger logger,
            string operationName,
            bool verbose = true,
            bool instantStart = false)
        {
            Preconditions.CheckArgument(period > TimeSpan.Zero, "period should be > 0");
            Preconditions.CheckArgument(startAfter >= TimeSpan.Zero, "startAfter should be >= 0");

            this.work = Preconditions.CheckNotNull(work, nameof(work));
            this.period = period;
            this.startAfter = startAfter;
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.operationName = Preconditions.CheckNonWhiteSpace(operationName, nameof(operationName));
            this.currentTask = this.DoWork();
            this.checkTimer = new Timer(this.EnsureWork, null, startAfter, period);
            this.verbose = verbose;
            this.instantStart = instantStart;
            this.logger.LogInformation($"Started operation {this.operationName}");
        }

        public PeriodicTask(
            Func<Task> work,
            TimeSpan period,
            TimeSpan startAfter,
            ILogger logger,
            string operationName,
            bool verbose = true, bool instantStart = false)
            : this(_ => Preconditions.CheckNotNull(work, nameof(work))(), period, startAfter, logger, operationName, verbose, instantStart)
        { }

        /// <summary>
        /// Do not dispose the task here in case it hasn't completed.
        /// </summary>
        public void Dispose()
        {
            this.checkTimer?.Dispose();
            this.cts?.Cancel();
            this.cts?.Dispose();
        }

        /// <summary>
        /// The current task should never complete, but in case it does, this makes sure it is started again.
        /// </summary>
        void EnsureWork(object state)
        {
            lock (this.stateLock)
            {
                if (this.currentTask == null || this.currentTask.IsCompleted)
                {
                    this.logger.LogInformation($"Periodic operation {this.operationName}, is not running. Attempting to start again...");
                    this.currentTask = this.DoWork();
                    this.logger.LogInformation($"Started operation {this.operationName}");
                }
            }
        }

        async Task DoWork()
        {
            try
            {
                CancellationToken cancellationToken = this.cts.Token;
                if (! instantStart)
                    await Task.Delay(this.startAfter, cancellationToken);
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // void Log(string message) => this.verbose ? this.logger.LogInformation(message) : this.logger.LogDebug(message);
                        void Log(string message)
                        {
                            if (this.verbose)
                            {
                                this.logger.LogInformation(message);
                            }
                            else
                            {
                                this.logger.LogDebug(message);
                            }
                        }

                        Log($"Starting periodic operation {this.operationName}...");
                        await this.work(cancellationToken);
                        Log($"Successfully completed periodic operation {this.operationName}");
                    }
                    catch (Exception e)
                    {
                        this.logger.LogWarning(e, $"Error in periodic operation {this.operationName}");
                        TelemClient.TrackTaggedException(e);
                    }

                    await Task.Delay(this.period, cancellationToken);
                }

                this.logger.LogWarning($"Periodic operation {this.operationName} cancelled");
                TelemClient.TrackTaggedEvent($"Periodic operation {this.operationName} cancelled");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Unexpected error in periodic operation {this.operationName}");
                TelemClient.TrackTaggedException(ex);
            }
        }
    }
}
