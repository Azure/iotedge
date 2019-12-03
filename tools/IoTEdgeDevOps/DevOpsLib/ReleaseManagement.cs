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


    public class ReleaseManagement
    {
        const string ReleasePathSegmentFormat = "{0}/{1}/_apis/release/releases";

        readonly DevOpsAccessSetting accessSetting;

        public ReleaseManagement(DevOpsAccessSetting accessSetting)
        {
            this.accessSetting = accessSetting;
        }

        /// <summary>
        /// This method is used to get latest release result of given release definition Id and branch name with descending order.
        /// Reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/release/releases/list?view=azure-devops-rest-5.1
        /// </summary>
        /// <param name="definitionId">release definition Ids</param>
        /// <param name="branchName">github repository branch name</param>
        /// <returns>List of IoT Edge releases</returns>
        public async Task<List<IoTEdgeRelease>> GetReleasesAsync(ReleaseDefinitionId definitionId, string branchName, int top = 5)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(branchName, nameof(branchName));
            ValidationUtil.ThrowIfNonPositive(top, nameof(top));

            // TODO: need to think about how to handle unexpected exception during REST API call
            string requestPath = string.Format(ReleasePathSegmentFormat, this.accessSetting.Organization, this.accessSetting.Project);
            IFlurlRequest latestBuildRequest = DevOpsAccessSetting.ReleaseManagementBaseUrl
                .AppendPathSegment(requestPath)
                .SetQueryParam("definitionId", definitionId.IdString())
                .SetQueryParam("queryOrder", "descending")
                .SetQueryParam("$expand", "environments")
                .SetQueryParam("statusFilter", "active")
                .SetQueryParam("$top", top)
                .SetQueryParam("api-version", "5.1")
                .SetQueryParam("sourceBranchFilter", branchName)
                .WithBasicAuth(string.Empty, this.accessSetting.PersonalAccessToken);

            string resultJson = await latestBuildRequest.GetStringAsync().ConfigureAwait(false);
            JObject result = JObject.Parse(resultJson);

            if (!result.ContainsKey("count") || (int) result["count"] <= 0)
            {
                return new List<IoTEdgeRelease>();
            }

            VstsRelease[] releases = JsonConvert.DeserializeObject<VstsRelease[]>(result["value"].ToString());
            return releases.Select(IoTEdgeRelease.Create).ToList();
        }
    }
}
