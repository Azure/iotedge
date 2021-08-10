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
        readonly HashSet<BugWiqlQuery> bugQueries;

        public VstsBuildBatchUpdate(DevOpsAccessSetting devOpsAccessSetting, string dbConnectionString, HashSet<string> branches, HashSet<BugWiqlQuery> bugQueries)
        {
            ValidationUtil.ThrowIfNull(devOpsAccessSetting, nameof(devOpsAccessSetting));
            ValidationUtil.ThrowIfNullOrEmptySet(branches, nameof(branches));
            ValidationUtil.ThrowIfNullOrEmptySet(bugQueries, nameof(VstsBuildBatchUpdate.bugQueries));

            this.devOpsAccessSetting = devOpsAccessSetting;
            this.dbConnectionString = dbConnectionString;
            this.buildLastUpdatePerBranchPerDefinition = new Dictionary<string, Dictionary<BuildDefinitionId, DateTime>>();
            this.branches = branches;
            this.bugQueries = bugQueries;
        }

        public async Task RunAsync(TimeSpan waitPeriodAfterEachUpdate, CancellationToken ct)
        {
            var buildManagement = new BuildManagement(devOpsAccessSetting);
            var releaseManagement = new ReleaseManagement(devOpsAccessSetting);
            var bugWiqlManagement = new BugWiqlManagement(devOpsAccessSetting);

            var userManagement = new UserManagement(devOpsAccessSetting);
            var commitManagement = new CommitManagement();
            var bugManagement = new BugManagement(devOpsAccessSetting, commitManagement, userManagement);

            while (!ct.IsCancellationRequested)
            {
                await ImportVstsBugDataAsync(bugWiqlManagement, bugQueries);

                foreach (string branch in this.branches)
                {
                    foreach (BuildDefinitionId buildDefinitionId in BuildExtension.BuildDefinitions)
                    {
                        IList<VstsBuild> builds = await GetBuildsAndTrackLastUpdatedAsync(buildManagement, buildDefinitionId, branch);
                        Console.WriteLine($"Received {builds.Count} builds");
                        ImportVstsBuildsDataForSpecificDefinitionAsync(builds, buildDefinitionId);
                        await OpenBugsForFailingBuilds(bugManagement, builds, branch, buildDefinitionId);
                    }
                }

                foreach (string branch in this.branches)
                {
                    await ImportVstsReleasesDataAsync(releaseManagement, branch, ReleaseDefinitionId.E2ETest);
                }

                Console.WriteLine($"Import Vsts data finished at {DateTime.UtcNow}; wait {waitPeriodAfterEachUpdate} for next update.");
                await Task.Delay((int)waitPeriodAfterEachUpdate.TotalMilliseconds);
            }
        }

        async Task OpenBugsForFailingBuilds(BugManagement bugManagement, IList<VstsBuild> builds, string branch, BuildDefinitionId buildDefinitionId)
        {
            // Filter out the builds for which we have already made bugs
            builds = FilterBuildsByDate(builds);
            builds = FilterBuildsByStatus(builds);
            builds = FilterBuildsByExistingBugs(builds);
            Console.WriteLine($"Filtering builds complete. Creating bugs for {builds.Count} builds");

            // Create the bugs
            Dictionary<string, string> buildIdToBugId = new Dictionary<string, string>();
            foreach (VstsBuild build in builds)
            {
                try
                {
                    string bugId = await bugManagement.CreateBugAsync(branch, build);
                    buildIdToBugId.Add(build.BuildId, bugId);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine("Create bug failed. Will retry later.");
                }
            }

            if (buildIdToBugId.Count == 0)
            {
                return;
            }

            Console.WriteLine($"Successfully created {buildIdToBugId.Count} bugs for {buildDefinitionId.ToString()} on {branch} branch");

            // Add the created bugs to the db for tracking
            SqlConnection sqlConnection = null;
            try
            {
                sqlConnection = new SqlConnection(this.dbConnectionString);
                sqlConnection.Open();

                foreach ((string buildId, string bugId) in buildIdToBugId)
                {
                    UpsertVstsBugToDb(sqlConnection, buildId, bugId);
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

        async Task<IList<VstsBuild>> GetBuildsAndTrackLastUpdatedAsync(BuildManagement buildManagement, BuildDefinitionId buildDefinitionId, string branch)
        {
            Dictionary<BuildDefinitionId, DateTime> buildDefinitionIdToLastUpdate = this.buildLastUpdatePerBranchPerDefinition.GetIfExists(branch);
            if (buildDefinitionIdToLastUpdate == null)
            {
                buildDefinitionIdToLastUpdate = new Dictionary<BuildDefinitionId, DateTime>();
            }

            DateTime lastUpdate = buildDefinitionIdToLastUpdate.GetIfExists(buildDefinitionId);
            IList<VstsBuild> buildResults = await buildManagement.GetBuildsAsync(new HashSet<BuildDefinitionId> { buildDefinitionId }, branch, lastUpdate);
            Console.WriteLine($"Query VSTS builds for branch [{branch}] and build definition [{buildDefinitionId.ToString()}]: last update={lastUpdate} => result count={buildResults.Count}");

            DateTime maxLastChange = DateTime.MinValue;
            foreach (VstsBuild build in buildResults.Where(r => r.HasResult()))
            {
                if (build.LastChangedDate > maxLastChange)
                {
                    maxLastChange = build.LastChangedDate;
                }
            }

            buildDefinitionIdToLastUpdate.Upsert(buildDefinitionId, maxLastChange);
            this.buildLastUpdatePerBranchPerDefinition.Upsert(branch, buildDefinitionIdToLastUpdate);

            return buildResults;
        }
        async Task ImportVstsBugDataAsync(BugWiqlManagement bugWiqlManagement, HashSet<BugWiqlQuery> bugQueries)
        {
            Console.WriteLine($"Import VSTS bugs started at {DateTime.UtcNow}.");
            SqlConnection sqlConnection = null;

            try
            {
                sqlConnection = new SqlConnection(this.dbConnectionString);
                sqlConnection.Open();
                foreach (BugWiqlQuery bugQuery in bugQueries)
                {
                    int bugCount = await bugWiqlManagement.GetBugsCountAsync(bugQuery);

                    Console.WriteLine($"Query VSTS bugs for area [{bugQuery.Area}] and priority [{bugQuery.BugPriorityGrouping.Priority}] and inProgress [{bugQuery.InProgress}]: last update={DateTime.UtcNow} => result count={bugCount}");
                    UpsertVstsBugCountToDb(sqlConnection, bugQuery, bugCount);
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

        void ImportVstsBuildsDataForSpecificDefinitionAsync(
            IList<VstsBuild> builds,
            BuildDefinitionId buildDefinitionId)
        {
            SqlConnection sqlConnection = null;

            try
            {
                sqlConnection = new SqlConnection(this.dbConnectionString);
                sqlConnection.Open();

                foreach (VstsBuild build in builds.Where(r => r.HasResult()))
                {
                    UpsertVstsBuildToDb(sqlConnection, build);
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

        void UpsertVstsBugToDb(SqlConnection sqlConnection, string buildId, string bugId)
        {
            var cmd = new SqlCommand
            {
                Connection = sqlConnection,
                CommandType = CommandType.StoredProcedure,
                CommandText = "UpsertVstsBug"
            };

            cmd.Parameters.Add(new SqlParameter("@BuildId", buildId));
            cmd.Parameters.Add(new SqlParameter("@BugId", bugId));

            cmd.ExecuteNonQuery();
        }
        IList<VstsBuild> FilterBuildsByExistingBugs(IList<VstsBuild> builds)
        {
            IList<VstsBuild> filteredBuilds = new List<VstsBuild>();
            SqlConnection sqlConnection = null;
            try
            {
                sqlConnection = new SqlConnection(this.dbConnectionString);
                sqlConnection.Open();

                foreach (VstsBuild build in builds)
                {
                    var cmd = new SqlCommand
                    {
                        Connection = sqlConnection,
                        CommandType = CommandType.StoredProcedure,
                        CommandText = "QueryVstsBugsForMatchingBuild"
                    };

                    cmd.Parameters.Add(new SqlParameter("@BuildId", build.BuildId));

                    SqlDataReader reader = cmd.ExecuteReader();
                    if (!reader.HasRows)
                    {
                        filteredBuilds.Add(build);
                    }

                    reader.Close();
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

            Console.WriteLine($"Filtering by existing bugs complete. {filteredBuilds.Count} builds remaining.");

            return filteredBuilds;
        }

        // We don't want to spam making bugs.
        // Given this logic runs at least every few minutes, only make a bug for builds in the past 1 hour.
        IList<VstsBuild> FilterBuildsByDate(IList<VstsBuild> builds)
        {
            IList<VstsBuild> filteredBuilds = new List<VstsBuild>();
            foreach (VstsBuild build in builds)
            {
                if (build.FinishTime > DateTime.UtcNow - TimeSpan.FromHours(1))
                {
                    filteredBuilds.Add(build);
                }
            }

            Console.WriteLine($"Filtering by date complete. {filteredBuilds.Count} builds remaining.");
            return filteredBuilds;
        }

        IList<VstsBuild> FilterBuildsByStatus(IList<VstsBuild> builds)
        {
            IList<VstsBuild> filteredBuilds = new List<VstsBuild>();
            foreach (VstsBuild build in builds)
            {
                if (build.Result == VstsBuildResult.Failed && build.WasScheduled() == true)
                {
                    filteredBuilds.Add(build);
                }
            }

            Console.WriteLine($"Filtering out non-scheduled builds complete. {filteredBuilds.Count} builds remaining.");
            return filteredBuilds;
        }

        void UpsertVstsBugCountToDb(SqlConnection sqlConnection, BugWiqlQuery bugQuery, int bugCount)
        {
            var cmd = new SqlCommand
            {
                Connection = sqlConnection,
                CommandType = CommandType.StoredProcedure,
                CommandText = "UpsertVstsBugCounts"
            };

            cmd.Parameters.Add(new SqlParameter("@Title", bugQuery.Title));
            cmd.Parameters.Add(new SqlParameter("@AreaPath", bugQuery.Area));
            cmd.Parameters.Add(new SqlParameter("@Priority", bugQuery.BugPriorityGrouping.Priority));
            cmd.Parameters.Add(new SqlParameter("@InProgress", bugQuery.InProgress));
            cmd.Parameters.Add(new SqlParameter("@BugCount", bugCount));

            cmd.ExecuteNonQuery();
        }

        void UpsertVstsBuildToDb(SqlConnection sqlConnection, VstsBuild build)
        {
            var cmd = new SqlCommand
            {
                Connection = sqlConnection,
                CommandType = CommandType.StoredProcedure,
                CommandText = "UpsertVstsBuild"
            };

            cmd.Parameters.Add(new SqlParameter("@BuildId", build.BuildId));
            cmd.Parameters.Add(new SqlParameter("@BuildNumber", build.BuildNumber));
            cmd.Parameters.Add(new SqlParameter("@DefinitionId", build.DefinitionId));
            cmd.Parameters.Add(new SqlParameter("@DefinitionName", build.DefinitionId.DisplayName()));
            cmd.Parameters.Add(new SqlParameter("@SourceBranch", build.SourceBranch));
            cmd.Parameters.Add(new SqlParameter("@SourceVersion", build.SourceVersion));
            cmd.Parameters.Add(new SqlParameter("@SourceVersionDisplayUri", build.SourceVersionDisplayUri.AbsoluteUri));
            cmd.Parameters.Add(new SqlParameter("@WebUri", build.WebUri.AbsoluteUri));
            cmd.Parameters.Add(new SqlParameter("@Status", build.Status.ToString()));
            cmd.Parameters.Add(new SqlParameter("@Result", build.Result.ToString()));
            cmd.Parameters.Add(new SqlParameter("@QueueTime", SqlDbType.DateTime2) { Value = build.QueueTime });
            cmd.Parameters.Add(new SqlParameter("@StartTime", SqlDbType.DateTime2) { Value = build.StartTime });
            cmd.Parameters.Add(new SqlParameter("@FinishTime", SqlDbType.DateTime2) { Value = build.FinishTime });
            cmd.Parameters.Add(new SqlParameter("@WasScheduled", build.WasScheduled().ToString()));

            cmd.ExecuteNonQuery();
        }

        async Task ImportVstsReleasesDataAsync(ReleaseManagement releaseManagement, string branch, ReleaseDefinitionId releaseDefinitionId)
        {
            Console.WriteLine($"Import VSTS releases from branch [{branch}] started at {DateTime.UtcNow}.");

            SqlConnection sqlConnection = null;

            try
            {
                sqlConnection = new SqlConnection(this.dbConnectionString);
                sqlConnection.Open();

                List<IoTEdgeRelease> releaseResults = await releaseManagement.GetReleasesAsync(releaseDefinitionId, branch, 200);
                Console.WriteLine($"Query VSTS releases for branch [{branch}] and release definition [{releaseDefinitionId.ToString()}]: result count={releaseResults.Count} at {DateTime.UtcNow}.");

                int releaseCount = 0;

                foreach (IoTEdgeRelease release in releaseResults.Where(r => r.HasResult()))
                {
                    UpsertVstsReleaseToDb(sqlConnection, release);

                    foreach (KeyValuePair<int, string> kvp in ReleaseEnvironment.DefinitionIdToDisplayNameMapping)
                    {
                        IoTEdgeReleaseEnvironment releaseEnvironment = release.GetEnvironment(kvp.Key);

                        if (releaseEnvironment.HasResult())
                        {
                            UpsertVstsReleaseEnvironmentToDb(sqlConnection, release.Id, releaseEnvironment, kvp.Value);

                            foreach (IoTEdgeReleaseDeployment deployment in releaseEnvironment.Deployments)
                            {
                                UpsertVstsReleaseDeploymentToDb(sqlConnection, releaseEnvironment.Id, deployment);

                                const string testTaskPrefix = "Test:";

                                foreach (IoTEdgePipelineTask pipelineTask in deployment.Tasks.Where(x => IsTestTask(x, testTaskPrefix)))
                                {
                                    UpsertVstsReleaseTaskToDb(sqlConnection, deployment.Id, pipelineTask, testTaskPrefix);
                                }
                            }
                        }
                    }

                    releaseCount++;

                    if (releaseCount % 10 == 0)
                    {
                        Console.WriteLine($"Query VSTS releases for branch [{branch}] and release definition [{releaseDefinitionId.ToString()}]: release count={releaseCount} at {DateTime.UtcNow}.");
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

        bool IsTestTask(IoTEdgePipelineTask pipelineTask, string testTaskPrefix)
        {
            return pipelineTask.Name.StartsWith(testTaskPrefix);
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
            cmd.Parameters.Add(new SqlParameter("@LastModifiedOn", SqlDbType.DateTime2) { Value = deployment.LastModifiedOn });
            cmd.ExecuteNonQuery();
        }

        void UpsertVstsReleaseTaskToDb(SqlConnection sqlConnection, int releaseDeploymentId, IoTEdgePipelineTask task, string testTaskPrefix)
        {
            var cmd = new SqlCommand
            {
                Connection = sqlConnection,
                CommandType = CommandType.StoredProcedure,
                CommandText = "UpsertVstsReleaseTask"
            };

            cmd.Parameters.Add(new SqlParameter("@ReleaseDeploymentId", releaseDeploymentId));
            cmd.Parameters.Add(new SqlParameter("@Id", task.Id));
            cmd.Parameters.Add(new SqlParameter("@Name", task.Name.StartsWith(testTaskPrefix) ? task.Name.Substring(testTaskPrefix.Length) : task.Name));
            cmd.Parameters.Add(new SqlParameter("@Status", task.Status));
            cmd.Parameters.Add(new SqlParameter("@StartTime", SqlDbType.DateTime2) { Value = task.StartTime });
            cmd.Parameters.Add(new SqlParameter("@FinishTime", SqlDbType.DateTime2) { Value = task.FinishTime });
            cmd.Parameters.Add(new SqlParameter("@LogUrl", task.LogUrl?.AbsoluteUri));
            cmd.ExecuteNonQuery();
        }
    }
}
