// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using RunProcessAsTask;

    class CommandExecutor
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<CommandExecutor>();

        public static async Task<string> Execute(string commandName, string args, CancellationToken cs)
        {
            var info = new ProcessStartInfo
            {
                FileName = commandName,
                Arguments = args
            };

            using (ProcessResults result = await ProcessEx.RunAsync(info, cs))
            {
                if (result.ExitCode != 0)
                {
                    string errorMessage = $"{commandName} result: {result.StandardError.Join(" ")}";
                    Log.LogError(errorMessage);
                    throw new CommandExecutionException(errorMessage);
                }

                Log.LogDebug($"{commandName} result: {result.StandardOutput.Join(" ")}");
                return result.StandardOutput.Join(" ");
            }
        }
    }
}
