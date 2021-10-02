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
        public static async Task<string[]> RunAsync(string name, string args, CancellationToken token, bool logVerbose = true)
        {
            var info = new ProcessStartInfo
            {
                FileName = name,
                Arguments = args,
                RedirectStandardInput = true,
            };

            Action<string> onStdout = (string o) => Log.Verbose(o);
            Action<string> onStderr = (string e) => Log.Verbose(e);

            if (!logVerbose)
            {
                onStdout = (string o) => { };
                onStderr = (string e) => { };
            }

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
