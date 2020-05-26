namespace VstsPipelineSync
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DevOpsLib;
    using Microsoft.Azure.Services.AppAuthentication;
    using Microsoft.Azure.KeyVault;

    class Program
    {
        static async Task Main(string[] args)
        {
            (HashSet<string> branches, TimeSpan waitPeriodBeforeNextUpdate, string pat, string dbConnectionString) = GetInputsFromArgs(args);
            Console.WriteLine($"Wait period before next update=[{waitPeriodBeforeNextUpdate}]");
            
            HashSet<BugQuery> bugQueries = BugQueryGenerator.GenerateBugQueries();

            VstsBuildBatchUpdate vstsBuildBatchUpdate = new VstsBuildBatchUpdate(new DevOpsAccessSetting(pat), dbConnectionString, branches, bugQueries);
            await vstsBuildBatchUpdate.RunAsync(waitPeriodBeforeNextUpdate, CancellationToken.None);
        }

        private static (HashSet<string> branches, TimeSpan waitPeriodBeforeNextUpdate, string pat, string dbConnectionString) GetInputsFromArgs(string[] args)
        {
            if (args.Length != 2 && args.Length != 4)
            {
                Console.WriteLine("*** This program will ingest vsts data and upload to the database used by the iotedge test dashboard.");
                Console.WriteLine("By default, it will authenticate with the database and vsts using secrets from keyvault. You can also handle the auth yourself using command line args.");
                Console.WriteLine("VstsBuildBatchUpdate.exe <branches> <wait-period> [<vsts-pat> <db-connection-string>] ");
                Console.WriteLine("Usage:");
                Console.WriteLine(" branches: comma deliminated name of branches");
                Console.WriteLine(" wait-period: time between db updates (e.g. 00:01:00)");
                Console.WriteLine(" vsts-pat: personal access token to vsts");
                Console.WriteLine(" db-connection-string: sql server connection string found in the azure portal");
                Environment.Exit(1);
            }

            HashSet<string> branches = new HashSet<string>(args[0].Split(","));
            TimeSpan waitPeriodBeforeNextUpdate = TimeSpan.Parse(args[1]);
            string pat;
            string dbConnectionString;

            if (args.Length == 4)
            {
                pat = args[2];
                dbConnectionString = args[3];
            }
            else
            {
               pat = GetSecretFromKeyVault_ManagedIdentity_TokenProvider("TestDashboardVstsPat");
               dbConnectionString = GetSecretFromKeyVault_ManagedIdentity_TokenProvider("TestDashboardDbConnectionString");
            }

            return (branches, waitPeriodBeforeNextUpdate, pat, dbConnectionString);
        }

        // Reference from https://zimmergren.net/azure-container-instances-managed-identity-key-vault-dotnet-core/
        private static string GetSecretFromKeyVault_ManagedIdentity_TokenProvider(string secretName)
        {
            Console.WriteLine($"Getting secret from keyvault: {secretName}");

            AzureServiceTokenProvider tokenProvider = new AzureServiceTokenProvider();
            var keyVault = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(tokenProvider.KeyVaultTokenCallback));
            var secretResult = keyVault.GetSecretAsync("https://edgebuildkv.vault.azure.net/", secretName).Result;

            return secretResult.Value;
        }
    }
}
