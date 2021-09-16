// Copyright (c) Microsoft. All rights reserved.

// Copyright (c) 2013 James Manning
// Adapted from https://github.com/jamesmanning/RunProcessAsTask
namespace RunProcessAsTask
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public static partial class ProcessEx
    {
        /// <summary>
        /// Runs asynchronous process.
        /// </summary>
        /// <param name="processStartInfo">The <see cref="T:System.Diagnostics.ProcessStartInfo" /> that contains the information that is used to start the process, including the file name and any command-line arguments.</param>
        /// <param name="onStandardOutput">Method that will be called when a line is written to standard output</param>
        /// <param name="onStandardError">Method that will be called when a line is written to standard error</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        public static async Task<ProcessResults> RunAsync(ProcessStartInfo processStartInfo, Action<string> onStandardOutput, Action<string> onStandardError, CancellationToken cancellationToken)
        {
            var standardOutput = new List<string>();
            var standardError = new List<string>();

            // force some settings in the start info so we can capture the output
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;

            var tcs = new TaskCompletionSource<ProcessResults>();

            var process = new Process
            {
                StartInfo = processStartInfo,
                EnableRaisingEvents = true
            };

            var standardOutputResults = new TaskCompletionSource<string[]>();
            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    standardOutput.Add(args.Data);
                    onStandardOutput(args.Data);
                }
                else
                {
                    standardOutputResults.SetResult(standardOutput.ToArray());
                }
            };

            var standardErrorResults = new TaskCompletionSource<string[]>();
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    standardError.Add(args.Data);
                    onStandardError(args.Data);
                }
                else
                {
                    standardErrorResults.SetResult(standardError.ToArray());
                }
            };

            var processStartTime = new TaskCompletionSource<DateTime>();

            process.Exited += async (sender, args) =>
            {
                // Since the Exited event can happen asynchronously to the output and error events,
                // we await the task results for stdout/stderr to ensure they both closed.  We must await
                // the stdout/stderr tasks instead of just accessing the Result property due to behavior on MacOS.
                // For more details, see the PR at https://github.com/jamesmanning/RunProcessAsTask/pull/16/
                tcs.TrySetResult(
                    new ProcessResults(
                        process,
                        await processStartTime.Task.ConfigureAwait(false),
                        await standardOutputResults.Task.ConfigureAwait(false),
                        await standardErrorResults.Task.ConfigureAwait(false)));
            };

            using (cancellationToken.Register(
                () =>
                {
                    tcs.TrySetCanceled();
                    try
                    {
                        if (!process.HasExited)
                            process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var startTime = DateTime.Now;
                if (process.Start() == false)
                {
                    tcs.TrySetException(new InvalidOperationException("Failed to start process"));
                }
                else
                {
                    try
                    {
                        startTime = process.StartTime;
                    }
                    catch (Exception)
                    {
                        // best effort to try and get a more accurate start time, but if we fail to access StartTime
                        // (for instance, process has already existed), we still have a valid value to use.
                    }

                    processStartTime.SetResult(startTime);

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }
    }
}