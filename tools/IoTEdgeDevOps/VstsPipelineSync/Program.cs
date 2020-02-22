namespace VstsPipelineSync
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DevOpsLib;

    class Program
    {
        static async Task Main(string[] args)
        {
            (string pat, string dbConnectionString, TimeSpan waitPeriodBeforeNextUpdate, HashSet<string> branches) = GetInputsFromArgs(args);
            Console.WriteLine($"Wait period before next update=[{waitPeriodBeforeNextUpdate}]");
            await new VstsBuildBatchUpdate(new DevOpsAccessSetting(pat), dbConnectionString, branches).RunAsync(waitPeriodBeforeNextUpdate, CancellationToken.None);
        }

        private static (string pat, string dbConnectionString, TimeSpan waitPeriodBeforeNextUpdate, HashSet<string> branches) GetInputsFromArgs(string[] args)
        {
            if (args.Length != 3 && args.Length != 4)
            {
                Console.WriteLine("*** Please provide only 4 parameters - VSTS personal access token, test dashboard database connection string, wait period before next update and branches (with comman delimiter).");
                Console.WriteLine("Master branch will be automatically included.");
                Console.WriteLine("VstsBuildBatchUpdate.exe <VSTA PAT> <test dashboard connection string> <wait period, e.g. 00:01:00>");
                Environment.Exit(1);
            }

            var branches = new HashSet<string> { "refs/heads/master" };
            if (args.Length == 4)
            {
                branches.UnionWith(args[3].Split(","));
            }

            return (args[0], args[1], TimeSpan.Parse(args[2]), branches);
        }
    }
}
