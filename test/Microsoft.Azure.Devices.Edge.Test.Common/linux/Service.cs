// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class Service
    {
        public static Task StartAsync(string[] names, CancellationToken token) =>
            Process.RunAsync("systemctl", $"start {string.Join(' ', names)}", token);

        public static Task StopAsync(string[] names, CancellationToken token) =>
            Process.RunAsync("systemctl", $"stop {string.Join(' ', names)}", token);

        public static async Task<bool> IsRunningAsync(string name, CancellationToken token)
        {
            string[] output = await Process.RunAsync("systemctl", $"-p ActiveState show {name}", token);
            return output.First().Split("=").Last() == "active";
        }

        public static async Task<bool> IsStoppedAsync(string name, CancellationToken token)
        {
            string[] output = await Process.RunAsync("systemctl", $"-p ActiveState show {name}", token);
            string status = output.First().Split("=").Last();
            return status == "inactive" || status == "failed";
        }
    }
}