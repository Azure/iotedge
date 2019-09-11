// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using RunProcessAsTask;
    using Serilog;

    public class Process
    {
        public static async Task<string[]> RunAsync(string name, string args, CancellationToken token)
        {
            var info = new ProcessStartInfo
            {
                FileName = name,
                Arguments = args,
                RedirectStandardInput = true,
            };

            Log.Information($">>> '{name} {args}'");

            using (ProcessResults result = await ProcessEx.RunAsync(info, token))
            {
                Log.Information($">>> '{name}' completed.'");

                if (result.ExitCode != 0)
                {
                    throw new Win32Exception(result.ExitCode, $"'{name}' failed with: {string.Join("\n", result.StandardError)}");
                }

                return result.StandardOutput;
            }
        }
    }
}
