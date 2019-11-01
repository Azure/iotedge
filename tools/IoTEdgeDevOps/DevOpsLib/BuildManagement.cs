// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Threading.Tasks;
    using DevOpsLib.VstsModels;
    using Flurl;
    using Flurl.Http;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class BuildManagement
    {
        public static VstsBuild EmptyBuild = new VstsBuild { Result = VstsBuildResult.None, Status = VstsBuildStatus.None };

        const string LatestBuildPathSegmentFormat = "{0}/{1}/_apis/build/latest/{2}";

        readonly DevOpsAccessSetting accessSetting;

        public BuildManagement(DevOpsAccessSetting accessSetting)
        {
            this.accessSetting = accessSetting;
        }

        public async Task<VstsBuild> GetLatestBuildAsync(int buildDefinitionId, string branchName)
        {
            string requestPath = string.Format(LatestBuildPathSegmentFormat, this.accessSetting.Organization, this.accessSetting.Project, buildDefinitionId);
            IFlurlRequest latestBuildRequest = DevOpsAccessSetting.BaseUrl
                .AppendPathSegment(requestPath)
                .SetQueryParam("api-version", "5.1-preview.1")
                .SetQueryParam("branchName", branchName)
                .WithBasicAuth(string.Empty, this.accessSetting.PersonalAccessToken);

            string resultJson = await latestBuildRequest.GetStringAsync().ConfigureAwait(false);
            JObject result = JObject.Parse(resultJson);

            if (result.ContainsKey("result"))
            {
                var latestBuild = JsonConvert.DeserializeObject<VstsBuild>(resultJson);
                return latestBuild;
            }

            return EmptyBuild;
        }
    }
}
