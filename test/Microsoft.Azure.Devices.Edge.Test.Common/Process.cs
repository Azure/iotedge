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

    public class Process
    {
        public static async Task<string[]> RunAsync(string name, string args, List<string> stdout, List<string> stderr, CancellationToken token)
        {
            var info = new ProcessStartInfo
            {
                FileName = name,
                Arguments = args,
                RedirectStandardInput = true,
            };

            try {
                using (ProcessResults result = await ProcessEx.RunAsync(info, stdout, stderr, token))
                {
                    if (result.ExitCode != 0)
                    {
                        throw new Win32Exception(result.ExitCode);
                    }

                    return result.StandardOutput;
                }
            }
            catch (TaskCanceledException e)
            {
                throw new TaskCanceledException(
                    $"\nOUTPUT:\n{String.Join("\n", stdout)}\n\nERROR\n{String.Join("\n", stderr)}",
                    e);
            }
        }

        public static Task<string[]> RunAsync(string name, string args, CancellationToken token) =>
            RunAsync(name, args, new List<string>(), new List<string>(), token);
    }
}
