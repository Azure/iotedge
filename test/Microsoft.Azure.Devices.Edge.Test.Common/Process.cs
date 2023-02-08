// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using RunProcessAsTask;
    using Serilog;

    public class Process
    {
        public static async Task RunAsync(string name, string args, Action<string> onStandardOutput, Action<string> onStandardError, CancellationToken token)
        {
            var info = new ProcessStartInfo
            {
                FileName = name,
                Arguments = args,
                RedirectStandardInput = true,
            };

            using (ProcessResults result = await ProcessEx.RunAsync(info, onStandardOutput, onStandardError, token))
            {
                if (result.ExitCode != 0)
                {
                    throw new Win32Exception(result.ExitCode);
                }
            }
        }

        public static async Task<string[]> RunAsync(string name, string args, CancellationToken token, bool logVerbose = true)
        {
            var info = new ProcessStartInfo
            {
                FileName = name,
                Arguments = args,
                RedirectStandardInput = true,
            };

            if (logVerbose)
            {
                Log.Verbose($"RunAsync: {name} {args}");
            }

            Action<string> MakeOutputHandler(bool logVerbose)
            {
                return logVerbose ? (string s) => Log.Verbose(s) : (string o) => { };
            }

            Action<string> onStdout = MakeOutputHandler(logVerbose);
            Action<string> onStderr = MakeOutputHandler(logVerbose);

            using (ProcessResults result = await ProcessEx.RunAsync(info, onStdout, onStderr, token))
            {
                if (result.ExitCode != 0)
                {
                    throw new Win32Exception(result.ExitCode);
                }

                return result.StandardOutput;
            }
        }
    }
}
