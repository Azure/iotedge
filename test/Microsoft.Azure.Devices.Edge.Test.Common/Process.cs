// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using RunProcessAsTask;

    public class Process
    {
        public static async Task<string[]> RunAsync(string name, string args, string[] stdout, string[] stderr, CancellationToken token)
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
            catch (Exception e)
            {
                e.Message += $"\nOUTPUT:\n{String.Join("\n", stdout)}\n\nERROR\n{String.Join("\n", stderr)}";
                throw e;
            }
        }

        public static async Task<string[]> RunAsync(string name, string args, CancellationToken token)
        {
            return RunAsync(name, args, new List<string>(), new List<string>(), token);
        }
    }
}
