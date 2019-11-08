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
        public static readonly Dictionary<int, string> DefinitionIdToDisplayNameMapping = new Dictionary<int, string>();

        const string LatestBuildPathSegmentFormat = "{0}/{1}/_apis/build/builds";

        readonly DevOpsAccessSetting accessSetting;

        public BuildManagement(DevOpsAccessSetting accessSetting)
        {
            this.accessSetting = accessSetting;
        }

        public async Task<IList<VstsBuild>> GetLatestBuildsAsync(HashSet<BuildDefinitionId> buildDefinitionIds, string branchName)
        {
            ValidationUtil.ThrowIfNulOrEmptySet(buildDefinitionIds, nameof(buildDefinitionIds));
            ValidationUtil.ThrowIfNullOrWhiteSpace(branchName, nameof(branchName));

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

            if (!result.ContainsKey("count") || (int)result["count"] <= 0)
            {
                return buildDefinitionIds.Select(i => VstsBuild.GetBuildWithNoResult(i, branchName)).ToList();
            }

            Dictionary<BuildDefinitionId, VstsBuild> latestBuilds = JsonConvert.DeserializeObject<VstsBuild[]>(result["value"].ToString()).ToDictionary(b => b.DefinitionId, b => b);
            return buildDefinitionIds.Select(i => latestBuilds.ContainsKey(i) ? latestBuilds[i] : VstsBuild.GetBuildWithNoResult(i, branchName)).ToList();
        }
    }
}
