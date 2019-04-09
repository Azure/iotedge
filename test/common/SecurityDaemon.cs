using System;
using System.Threading;
using System.Threading.Tasks;

namespace common
{
    public class SecurityDaemon
    {
        private string scriptDir;

        public SecurityDaemon(string scriptDir)
        {
            this.scriptDir = scriptDir;
        }

        public async Task UninstallAsync(CancellationToken token)
        {
            var commands = new string[]
            {
                $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1",
                "Uninstall-IoTEdge -Force"
            };
            string[] result = await Process.RunAsync("powershell", string.Join(";", commands), token);
            Console.WriteLine(string.Join("\n", result));
        }
    }
}
