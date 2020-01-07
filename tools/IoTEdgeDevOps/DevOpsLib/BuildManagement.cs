// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using DevOpsLib.VstsModels;
    using Flurl;
    using Flurl.Http;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class BuildManagement
    {
        const string LatestBuildPathSegmentFormat = "{0}/{1}/_apis/build/builds";

        readonly DevOpsAccessSetting accessSetting;

        public BuildManagement(DevOpsAccessSetting accessSetting)
        {
            this.accessSetting = accessSetting;
        }

        /// <summary>
        /// This method is used to get latest build result of given build definition Ids and branch name.
        /// The results should always contain same number of vsts build entity of given build definitions.
        /// If result is not found for a build definition Id, it will return vsts build entity with no result.
        /// Note: no validation of build definition ids is taken.
        /// Reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.1
        /// </summary>
        /// <param name="buildDefinitionIds">build definition Ids</param>
        /// <param name="branchName">github repository branch name</param>
        /// <returns>List of vsts build entities</returns>
        public async Task<IList<VstsBuild>> GetLatestBuildsAsync(HashSet<BuildDefinitionId> buildDefinitionIds, string branchName)
        {
            ValidationUtil.ThrowIfNulOrEmptySet(buildDefinitionIds, nameof(buildDefinitionIds));
            ValidationUtil.ThrowIfNullOrWhiteSpace(branchName, nameof(branchName));

            // TODO: need to think about how to handle unexpected exception during REST API call
            string requestPath = string.Format(LatestBuildPathSegmentFormat, this.accessSetting.Organization, this.accessSetting.Project);
            IFlurlRequest latestBuildRequest = DevOpsAccessSetting.BaseUrl
                .AppendPathSegment(requestPath)
                .SetQueryParam("definitions", string.Join(",", buildDefinitionIds.Select(b => b.IdString())))
                .SetQueryParam("queryOrder", "finishTimeDescending")
                .SetQueryParam("maxBuildsPerDefinition", "1")
                .SetQueryParam("api-version", "5.1")
                .SetQueryParam("branchName", branchName)
                .WithBasicAuth(string.Empty, this.accessSetting.PersonalAccessToken);

            string resultJson = await latestBuildRequest.GetStringAsync().ConfigureAwait(false);
            JObject result = JObject.Parse(resultJson);

            if (!result.ContainsKey("count") || (int) result["count"] <= 0)
            {
                return buildDefinitionIds.Select(i => VstsBuild.CreateBuildWithNoResult(i, branchName)).ToList();
            }

            Dictionary<BuildDefinitionId, VstsBuild> latestBuilds = JsonConvert.DeserializeObject<VstsBuild[]>(result["value"].ToString()).ToDictionary(b => b.DefinitionId, b => b);
            return buildDefinitionIds.Select(i => latestBuilds.ContainsKey(i) ? latestBuilds[i] : VstsBuild.CreateBuildWithNoResult(i, branchName)).ToList();
        }
    }
}
