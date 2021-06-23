// Copyright (c) Microsoft. All rights reserved.

// Copyright (c) 2013 James Manning
namespace RunProcessAsTask
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Contains information about process after it has exited.
    /// </summary>
    public sealed class ProcessResults : IDisposable
    {
        public ProcessResults(Process process, DateTime processStartTime, string[] standardOutput, string[] standardError)
        {
            this.Process = process;
            this.ExitCode = process.ExitCode;
            this.RunTime = process.ExitTime - processStartTime;
            this.StandardOutput = standardOutput;
            this.StandardError = standardError;
        }

        public Process Process { get; }
        public int ExitCode { get; }
        public TimeSpan RunTime { get; }
        public string[] StandardOutput { get; }
        public string[] StandardError { get; }
        public void Dispose()
        {
            this.Process.Dispose();
        }
    }
}