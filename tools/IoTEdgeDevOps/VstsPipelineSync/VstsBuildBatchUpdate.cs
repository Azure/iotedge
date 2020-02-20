namespace VstsPipelineSync
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DevOpsLib;
    using DevOpsLib.VstsModels;

    public class VstsBuildBatchUpdate
    {
        readonly DevOpsAccessSetting devOpsAccessSetting;
        readonly string dbConnectionString;
        Dictionary<string, Dictionary<BuildDefinitionId, DateTime>> lastUpdatePerBranchPerDefinition;
        HashSet<string> branches;

        public VstsBuildBatchUpdate(DevOpsAccessSetting devOpsAccessSetting, string dbConnectionString, HashSet<string> branches)
        {
            ValidationUtil.ThrowIfNull(devOpsAccessSetting, nameof(devOpsAccessSetting));
            ValidationUtil.ThrowIfNullOrEmptySet(branches, nameof(branches));

            this.devOpsAccessSetting = devOpsAccessSetting;
            this.dbConnectionString = dbConnectionString;
            this.lastUpdatePerBranchPerDefinition = new Dictionary<string, Dictionary<BuildDefinitionId, DateTime>>();
            this.branches = branches;
        }

        public async Task RunAsync(TimeSpan waitPeriodAfterEachUpdate, CancellationToken ct)
        {
            var buildManagement = new BuildManagement(devOpsAccessSetting);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine($"Import Vsts Builds data started at {DateTime.UtcNow}");

                    foreach (string branch in this.branches)
                    {
                        lastUpdatePerBranchPerDefinition.Upsert(
                            branch, 
                            await ImportVstsBuildsDataAsync(buildManagement, branch, BuildExtension.BuildDefinitions));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexcepted Exception: {ex}");
                }

                Console.WriteLine($"Import Vsts Builds data finished at {DateTime.UtcNow}; wait {waitPeriodAfterEachUpdate} for next update.");
                await Task.Delay((int)waitPeriodAfterEachUpdate.TotalMilliseconds);
            }
        }

        private async Task<Dictionary<BuildDefinitionId, DateTime>> ImportVstsBuildsDataAsync(BuildManagement buildManagement, string branch, HashSet<BuildDefinitionId> buildDefinitionIds)
        {
            Console.WriteLine($"Import VSTS builds from branch [{branch}]");
            Dictionary<BuildDefinitionId, DateTime> lastUpdatePerDefinition = this.lastUpdatePerBranchPerDefinition.GetIfExists(branch);
            if (lastUpdatePerDefinition == null)
            {
                lastUpdatePerDefinition = new Dictionary<BuildDefinitionId, DateTime>();
            }

            foreach (BuildDefinitionId buildDefinitionId in buildDefinitionIds)
            {
                DateTime maxLastChange = await ImportVstsBuildsDataForSpecificDefinitionAsync(buildManagement, branch, buildDefinitionId, lastUpdatePerDefinition.GetIfExists(buildDefinitionId));
                lastUpdatePerDefinition.Upsert(buildDefinitionId, maxLastChange);
            }

            return lastUpdatePerDefinition;
        }

        private async Task<DateTime> ImportVstsBuildsDataForSpecificDefinitionAsync(
            BuildManagement buildManagement,
            string branch,
            BuildDefinitionId buildDefinitionId,
            DateTime lastUpdate)
        {
            SqlConnection sqlConnection = null;

            try
            {
                sqlConnection = new SqlConnection(this.dbConnectionString);
                sqlConnection.Open();

                IList<VstsBuild> buildResults = await buildManagement.GetBuildsAsync(new HashSet<BuildDefinitionId> { buildDefinitionId }, branch, lastUpdate);
                Console.WriteLine($"Query VSTS for branch [{branch}] and build definition [{buildDefinitionId.ToString()}]: last update={lastUpdate} => result count={buildResults.Count}");
                DateTime maxLastChange = DateTime.MinValue;

                foreach (VstsBuild build in buildResults.Where(r => r.HasResult()))
                {
                    var cmd = new SqlCommand
                    {
                        Connection = sqlConnection,
                        CommandType = System.Data.CommandType.StoredProcedure,
                        CommandText = "UpsertVstsBuild"
                    };

                    cmd.Parameters.Add(new SqlParameter("@BuildNumber", build.BuildNumber));
                    cmd.Parameters.Add(new SqlParameter("@DefinitionId", build.DefinitionId));
                    cmd.Parameters.Add(new SqlParameter("@DefinitionName", build.DefinitionId.DisplayName()));
                    cmd.Parameters.Add(new SqlParameter("@SourceBranch", build.SourceBranch));
                    cmd.Parameters.Add(new SqlParameter("@SourceVersionDisplayUri", build.SourceVersionDisplayUri.AbsoluteUri));
                    cmd.Parameters.Add(new SqlParameter("@WebUri", build.WebUri.AbsoluteUri));
                    cmd.Parameters.Add(new SqlParameter("@Status", build.Status.ToString()));
                    cmd.Parameters.Add(new SqlParameter("@Result", build.Result.ToString()));
                    cmd.Parameters.Add(new SqlParameter("@QueueTime", SqlDbType.DateTime2) { Value=build.QueueTime } );
                    cmd.Parameters.Add(new SqlParameter("@StartTime", SqlDbType.DateTime2) { Value = build.StartTime });
                    cmd.Parameters.Add(new SqlParameter("@FinishTime", SqlDbType.DateTime2) { Value = build.FinishTime });

                    cmd.ExecuteNonQuery();

                    if (build.LastChangedDate > maxLastChange)
                    {
                        maxLastChange = build.LastChangedDate;
                    }
                }

                return maxLastChange;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                sqlConnection?.Close();
            }
        }
    }
}
