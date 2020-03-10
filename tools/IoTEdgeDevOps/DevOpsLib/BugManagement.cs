// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using DevOpsLib.VstsModels;
    using Flurl;
    using Flurl.Http;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class BugManagement
    {
        const string WorkItemPathSegmentFormat = "{0}/{1}/{2}/_apis/wit/wiql/{3}";

        readonly DevOpsAccessSetting accessSetting;

        public BugManagement(DevOpsAccessSetting accessSetting)
        {
            this.accessSetting = accessSetting;
        }

        /// <summary>
        /// This method is used to execute a Dev Ops work item query and get a list of bugs. 
        /// If result is not found for a query Id, it will return an entity with no result.
        /// Note: there is no validation of work item query ids.
        /// Reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.1
        /// </summary>
        /// <param name="buildDefinitionIds">build definition Ids</param>
        /// <param name="branchName">github repository branch name</param>
        /// <returns>List of vsts build entities</returns>
        public async Task<IList<VstsBuild>> GetLatestBuildsAsync(WorkItemQueryId workItemQueryId)
        {
            ValidationUtil.ThrowIfNullOrEmptySet(workItemQueryId, nameof(workItemQueryId));

            // TODO: need to think about how to handle unexpected exception during REST API call
            string requestPath = string.Format(WorkItemPathSegmentFormat, this.accessSetting.Organization, this.accessSetting.Project, this.accessSetting.Team, workItemQueryId);
            IFlurlRequest latestBuildRequest = GetBugsRequestUri(workItemQueryId, requestPath)
                .WithBasicAuth(string.Empty, this.accessSetting.PersonalAccessToken);

            string resultJson = await latestBuildRequest.GetStringAsync().ConfigureAwait(false);
            JObject result = JObject.Parse(resultJson);

            if (!result.ContainsKey("queryType"))
            {
                return buildDefinitionIds.Select(i => VstsBuild.CreateBuildWithNoResult(i, branchName)).ToList();
            }

            Dictionary<BuildDefinitionId, VstsBuild> latestBuilds = JsonConvert.DeserializeObject<VstsBuild[]>(result["value"].ToString()).ToDictionary(b => b.DefinitionId, b => b);
            return buildDefinitionIds.Select(i => latestBuilds.ContainsKey(i) ? latestBuilds[i] : VstsBuild.CreateBuildWithNoResult(i, branchName)).ToList();
        }

        public async Task<IList<VstsBuild>> GetBuildsAsync(HashSet<BuildDefinitionId> buildDefinitionIds, string branchName, DateTime? minTime = null, int? maxBuildsPerDefinition = null)
        {
            ValidationUtil.ThrowIfNullOrEmptySet(buildDefinitionIds, nameof(buildDefinitionIds));
            ValidationUtil.ThrowIfNullOrWhiteSpace(branchName, nameof(branchName));

            // TODO: need to think about how to handle unexpected exception during REST API call
            string requestPath = string.Format(LatestBuildPathSegmentFormat, this.accessSetting.Organization, this.accessSetting.Project);
            IFlurlRequest latestBuildRequest = GetBuildsRequestUri(buildDefinitionIds, branchName, requestPath, minTime, maxBuildsPerDefinition)
                .WithBasicAuth(string.Empty, this.accessSetting.PersonalAccessToken);

            string resultJson = await latestBuildRequest.GetStringAsync().ConfigureAwait(false);
            JObject result = JObject.Parse(resultJson);

            if (!result.ContainsKey("count") || (int)result["count"] <= 0)
            {
                return buildDefinitionIds.Select(i => VstsBuild.CreateBuildWithNoResult(i, branchName)).ToList();
            }

            return JsonConvert.DeserializeObject<VstsBuild[]>(result["value"].ToString()).ToList();
        }

        private static Url GetBugsRequestUri(HashSet<BuildDefinitionId> buildDefinitionIds, string requestPath)
        {
            Url requestUri = DevOpsAccessSetting.BaseUrl.AppendPathSegment(requestPath);

            return requestUri;
        }
    }
}
