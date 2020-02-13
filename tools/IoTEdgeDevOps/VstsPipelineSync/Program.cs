namespace VstsPipelineSync
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DevOpsLib;

    class Program
    {
        static async Task Main(string[] args)
        {
            (string pat, string dbConnectionString, TimeSpan waitPeriodBeforeNextUpdate) = GetInputsFromArgs(args);
            await new VstsBuildBatchUpdate(new DevOpsAccessSetting(pat), dbConnectionString).RunAsync(waitPeriodBeforeNextUpdate, CancellationToken.None);
        }

        private static (string pat, string dbConnectionString, TimeSpan waitPeriodBeforeNextUpdate) GetInputsFromArgs(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("*** Please provide only 3 parameters - VSTS personal access token, test dashboard database connection string, and wait period before next update.");
                Environment.Exit(1);
            }

            return (args[0], args[1], TimeSpan.Parse(args[2]));
        }
    }
}
