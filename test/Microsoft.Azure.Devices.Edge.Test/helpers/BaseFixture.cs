﻿// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;
    using Serilog;

    public class BaseFixture
    {
        CancellationTokenSource cts;
        Profiler profiler;
        DateTime testStartTime;

        protected CancellationToken TestToken => this.cts.Token;

        protected virtual Task BeforeTestTimerStarts() => Task.CompletedTask;

        [SetUp]
        protected async Task BeforeEachTestAsync()
        {
            await this.BeforeTestTimerStarts();
            this.cts = new CancellationTokenSource(Context.Current.TestTimeout);

            // await Process.RunAsync("powershell", "'=========='; 'WHO AM I?'; whoami", this.cts.Token);
            // await Process.RunAsync("powershell", "'=========='; foreach($file in ls 'C:\\ProgramData\\iotedge\\hsm\\enc_keys') { $file.FullName; $acl = Get-Acl $file.FullName; $acl.Group; $acl.Owner; $acl.AccessToString; '' }", this.cts.Token);
            // await Process.RunAsync("powershell", "'=========='", this.cts.Token);

            this.testStartTime = DateTime.Now;
            this.profiler = Profiler.Start();
            Log.Information("Running test '{Name}'", TestContext.CurrentContext.Test.Name);
        }

        [TearDown]
        protected async Task AfterEachTestAsync()
        {
            this.profiler.Stop("Completed test '{Name}'", TestContext.CurrentContext.Test.Name);
            await Profiler.Run(
                async () =>
                {
                    this.cts.Dispose();

                    if (TestContext.CurrentContext.Result.Outcome != ResultState.Ignored)
                    {
                        using var cts = new CancellationTokenSource(Context.Current.TeardownTimeout);
                        await NUnitLogs.CollectAsync(this.testStartTime, cts.Token);
                    }
                },
                "Completed test teardown");
        }
    }
}
