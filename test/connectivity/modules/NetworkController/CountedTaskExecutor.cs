// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    class CountedTaskExecutor
    {
        readonly TimeSpan interval;
        readonly ILogger logger;
        readonly string operationName;
        readonly int runCount;
        readonly TimeSpan startAfter;
        readonly Func<CancellationToken, Task> work;

        public CountedTaskExecutor(
            Func<CancellationToken, Task> work,
            TimeSpan startAfter,
            TimeSpan interval,
            int runsCount,
            ILogger logger,
            string operationName)
        {
            this.startAfter = startAfter;
            this.interval = interval;
            this.runCount = runsCount;
            this.work = work;
            this.logger = logger;
            this.operationName = operationName;
        }

        public async Task Schedule(CancellationToken cancellationToken)
        {
            await Task.Delay(this.startAfter, cancellationToken);

            for (int i = 0; i < this.runCount; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    this.logger.LogInformation($"Starting operation {this.operationName} run number {i}...");
                    await this.work(cancellationToken);
                    this.logger.LogInformation($"Successfully completed operation {this.operationName} run number {i}");
                    await Task.Delay(this.interval, cancellationToken);
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, $"Failed to run {this.operationName} scheduled task run number {i}");
                }
            }
        }
    }
}
