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
        readonly Dictionary<string, Dictionary<BuildDefinitionId, DateTime>> buildLastUpdatePerBranchPerDefinition;
        readonly HashSet<string> branches;

        public VstsBuildBatchUpdate(DevOpsAccessSetting devOpsAccessSetting, string dbConnectionString, HashSet<string> branches)
        {
            ValidationUtil.ThrowIfNull(devOpsAccessSetting, nameof(devOpsAccessSetting));
            ValidationUtil.ThrowIfNullOrEmptySet(branches, nameof(branches));

            this.devOpsAccessSetting = devOpsAccessSetting;
            this.dbConnectionString = dbConnectionString;
            this.buildLastUpdatePerBranchPerDefinition = new Dictionary<string, Dictionary<BuildDefinitionId, DateTime>>();
            this.branches = branches;
        }

        public async Task RunAsync(TimeSpan waitPeriodAfterEachUpdate, CancellationToken ct)
        {
            var buildManagement = new BuildManagement(devOpsAccessSetting);
            var releaseManagement = new ReleaseManagement(devOpsAccessSetting);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine($"Import Vsts Builds data started at {DateTime.UtcNow}");

                    foreach (string branch in this.branches)
                    {
                        buildLastUpdatePerBranchPerDefinition.Upsert(
                            branch,
                            await ImportVstsBuildsDataAsync(buildManagement, branch, BuildExtension.BuildDefinitions));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexcepted Exception: {ex}");
                }

                try
                {
                    Console.WriteLine($"Import Vsts Releases data started at {DateTime.UtcNow}");

                    foreach (string branch in this.branches)
                    {
                        await ImportVstsReleasesDataAsync(releaseManagement, branch, ReleaseDefinitionId.E2ETest);
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

        async Task<Dictionary<BuildDefinitionId, DateTime>> ImportVstsBuildsDataAsync(BuildManagement buildManagement, string branch, HashSet<BuildDefinitionId> buildDefinitionIds)
        {
            Console.WriteLine($"Import VSTS builds from branch [{branch}]");
            Dictionary<BuildDefinitionId, DateTime> lastUpdatePerDefinition = this.buildLastUpdatePerBranchPerDefinition.GetIfExists(branch);

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

        async Task<DateTime> ImportVstsBuildsDataForSpecificDefinitionAsync(
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
                DateTime maxLastChange = lastUpdate;

                foreach (VstsBuild build in buildResults.Where(r => r.HasResult()))
                {
                    UpsertVstsBuildToDb(sqlConnection, build);

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

        void UpsertVstsBuildToDb(SqlConnection sqlConnection, VstsBuild build)
        {
            var cmd = new SqlCommand
            {
                Connection = sqlConnection,
                CommandType = CommandType.StoredProcedure,
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
            cmd.Parameters.Add(new SqlParameter("@QueueTime", SqlDbType.DateTime2) { Value = build.QueueTime });
            cmd.Parameters.Add(new SqlParameter("@StartTime", SqlDbType.DateTime2) { Value = build.StartTime });
            cmd.Parameters.Add(new SqlParameter("@FinishTime", SqlDbType.DateTime2) { Value = build.FinishTime });

            cmd.ExecuteNonQuery();
        }

        async Task ImportVstsReleasesDataAsync(ReleaseManagement releaseManagement, string branch, ReleaseDefinitionId releaseDefinitionId)
        {
            SqlConnection sqlConnection = null;

            try
            {
                sqlConnection = new SqlConnection(this.dbConnectionString);
                sqlConnection.Open();

                List<IoTEdgeRelease> releaseResults = await releaseManagement.GetReleasesAsync(releaseDefinitionId, branch, 200);
                Console.WriteLine($"Query VSTS for branch [{branch}] and release definition [{releaseDefinitionId.ToString()}]: result count={releaseResults.Count}");

                foreach (IoTEdgeRelease release in releaseResults.Where(r => r.HasResult()))
                {
                    UpsertVstsReleaseToDb(sqlConnection, release);

                    foreach (KeyValuePair<int, string> kvp in ReleaseEnvironment.DefinitionIdToDisplayNameMapping)
                    {
                        IoTEdgeReleaseEnvironment releaseEnvironment = release.GetEnvironment(kvp.Key);

                        if (releaseEnvironment.HasResult())
                        {
                            UpsertVstsReleaseEnvironmentToDb(sqlConnection, release.Id, releaseEnvironment, kvp.Value);

                            foreach(IoTEdgeReleaseDeployment deployment in releaseEnvironment.Deployments)
                            {
                                UpsertVstsReleaseDeploymentToDb(sqlConnection, releaseEnvironment.Id, deployment);

                                foreach(IoTEdgePipelineTask pipelineTask in deployment.Tasks.Where(x => IsTestTask(x)))
                                {
                                    UpsertVstsReleaseTaskToDb(sqlConnection, deployment.Id, pipelineTask);
                                }
                            }
                        }
                    }
                }
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

        bool IsTestTask(IoTEdgePipelineTask pipelineTask)
        {
            return pipelineTask.Name.StartsWith("E2E");
        }

        void UpsertVstsReleaseToDb(SqlConnection sqlConnection, IoTEdgeRelease release)
        {
            var cmd = new SqlCommand
            {
                Connection = sqlConnection,
                CommandType = CommandType.StoredProcedure,
                CommandText = "UpsertVstsRelease"
            };

            cmd.Parameters.Add(new SqlParameter("@Id", release.Id));
            cmd.Parameters.Add(new SqlParameter("@Name", release.DefinitionId.DisplayName()));
            cmd.Parameters.Add(new SqlParameter("@SourceBranch", release.SourceBranch));
            cmd.Parameters.Add(new SqlParameter("@Status", release.Status.ToString()));
            cmd.Parameters.Add(new SqlParameter("@WebUri", release.WebUri.AbsoluteUri));
            cmd.Parameters.Add(new SqlParameter("@DefinitionId", release.DefinitionId));
            cmd.Parameters.Add(new SqlParameter("@DefinitionName", release.DefinitionId.DisplayName()));
            cmd.ExecuteNonQuery();
        }

        void UpsertVstsReleaseEnvironmentToDb(SqlConnection sqlConnection, int releaseId, IoTEdgeReleaseEnvironment environment, string envrionmentName)
        {
            var cmd = new SqlCommand
            {
                Connection = sqlConnection,
                CommandType = CommandType.StoredProcedure,
                CommandText = "UpsertVstsReleaseEnvironment"
            };

            cmd.Parameters.Add(new SqlParameter("@Id", environment.Id));
            cmd.Parameters.Add(new SqlParameter("@ReleaseId", releaseId));
            cmd.Parameters.Add(new SqlParameter("@DefinitionId", environment.DefinitionId));
            cmd.Parameters.Add(new SqlParameter("@DefinitionName", envrionmentName));
            cmd.Parameters.Add(new SqlParameter("@Status", environment.Status.ToString()));
            cmd.ExecuteNonQuery();
        }

        void UpsertVstsReleaseDeploymentToDb(SqlConnection sqlConnection, int releaseEnvironmentId, IoTEdgeReleaseDeployment deployment)
        {
            var cmd = new SqlCommand
            {
                Connection = sqlConnection,
                CommandType = CommandType.StoredProcedure,
                CommandText = "UpsertVstsReleaseDeployment"
            };

            cmd.Parameters.Add(new SqlParameter("@Id", deployment.Id));
            cmd.Parameters.Add(new SqlParameter("@ReleaseEnvironmentId", releaseEnvironmentId));
            cmd.Parameters.Add(new SqlParameter("@Attempt", deployment.Attempt));
            cmd.Parameters.Add(new SqlParameter("@Status", deployment.Status.ToString()));
            cmd.Parameters.Add(new SqlParameter("@LastModifiedOn", deployment.LastModifiedOn));
            cmd.ExecuteNonQuery();
        }

        void UpsertVstsReleaseTaskToDb(SqlConnection sqlConnection, int releaseDeploymentId, IoTEdgePipelineTask task)
        {
            var cmd = new SqlCommand
            {
                Connection = sqlConnection,
                CommandType = CommandType.StoredProcedure,
                CommandText = "UpsertVstsReleaseTask"
            };

            cmd.Parameters.Add(new SqlParameter("@ReleaseDeploymentId", releaseDeploymentId));
            cmd.Parameters.Add(new SqlParameter("@Id", task.Id));
            cmd.Parameters.Add(new SqlParameter("@Name", task.Name));
            cmd.Parameters.Add(new SqlParameter("@Status", task.Status));
            cmd.Parameters.Add(new SqlParameter("@StartTime", task.StartTime));
            cmd.Parameters.Add(new SqlParameter("@FinishTime", task.FinishTime));
            cmd.Parameters.Add(new SqlParameter("@LogUrl", task.LogUrl.AbsoluteUri));
            cmd.ExecuteNonQuery();
        }
    }
}
