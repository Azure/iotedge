using System;
using System.Diagnostics;

namespace RunProcessAsTask
{
    /// <summary>
    /// Contains information about process after it has exited.
    /// </summary>
    public sealed class ProcessResults : IDisposable
    {
        public ProcessResults(Process process, DateTime processStartTime, string[] standardOutput, string[] standardError)
        {
            Process = process;
            ExitCode = process.ExitCode;
            RunTime = process.ExitTime - processStartTime;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        public Process Process { get; }
        public int ExitCode { get; }
        public TimeSpan RunTime { get; }
        public string[] StandardOutput { get; }
        public string[] StandardError { get; }
        public void Dispose() { Process.Dispose(); }
    }
}
