namespace Microsoft.Azure.Devices.Edge.Service
{
    using Microsoft.Extensions.Configuration;
    using System.Linq;
    using System.Threading.Tasks;
    using AgentService = Microsoft.Azure.Devices.Edge.Agent.Service.Program;
    using HubService = Microsoft.Azure.Devices.Edge.Hub.Service.Program;

    class Program
    {
        const string AgentConfigFileName = "appsettings_agent.json";

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile(AgentConfigFileName)
                .AddEnvironmentVariables()
                .Build();

            // start up the hub
            var hubTask = HubService.MainAsync(configuration);

            // start up the edge agent
            var agentTask = AgentService.MainAsync(configuration);

            // wait for both to terminate
            var results = await Task.WhenAll(agentTask, hubTask);

            return results.Aggregate((acc, n) => acc + n);
        }
    }
}
