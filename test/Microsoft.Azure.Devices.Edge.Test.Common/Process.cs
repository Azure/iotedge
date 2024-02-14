// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using RunProcessAsTask;
    using Serilog;

    public class Process
    {
        public static async Task RunAsync(string name, string args, Action<string> onStandardOutput, Action<string> onStandardError, CancellationToken token, bool logCommand = true)
        {
            var info = new ProcessStartInfo
            {
                FileName = name,
                Arguments = args,
                RedirectStandardInput = true,
            };

            if (logCommand)
            {
                Log.Verbose($"RunAsync: {name} {args}");
            }

            using (ProcessResults result = await ProcessEx.RunAsync(info, onStandardOutput, onStandardError, token))
            {
                if (result.ExitCode != 0)
                {
                    throw new Win32Exception(result.ExitCode);
                }
            }
        }

        static async Task<string[]> RunAsync(ProcessStartInfo processStartInfo, CancellationToken token, bool logOutput)
        {
            Action<string> MakeOutputHandler(bool logOutput)
            {
                return logOutput ? (string s) => Log.Verbose(s) : (string o) => { };
            }

            Action<string> onStdout = MakeOutputHandler(logOutput);
            Action<string> onStderr = MakeOutputHandler(logOutput);

            using (ProcessResults result = await ProcessEx.RunAsync(processStartInfo, onStdout, onStderr, token))
            {
                if (result.ExitCode != 0)
                {
                    throw new Win32Exception(result.ExitCode);
                }

                return result.StandardOutput;
            }
        }

        public static async Task<string[]> RunAsync(string name, string args, CancellationToken token, bool logCommand = true, bool logOutput = true)
        {
            var info = new ProcessStartInfo
            {
                FileName = name,
                Arguments = args,
                RedirectStandardInput = true,
            };

            if (logCommand)
            {
                Log.Verbose($"RunAsync: {name} {args}");
            }

            return await RunAsync(info, token, logOutput);
        }

        public static async Task<string[]> RunAsync(string name, ICollection<string> args, CancellationToken token, bool logCommand = true, bool logOutput = true)
        {
            var info = new ProcessStartInfo
            {
                FileName = name,
                RedirectStandardInput = true,
            };

            foreach (var arg in args)
            {
                info.ArgumentList.Add(arg);
            }

            if (logCommand)
            {
                Log.Verbose($"RunAsync: {name} {string.Join(' ', args)}");
            }

            return await RunAsync(info, token, logOutput);
        }
    }
}
