// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Collections.Generic;
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
        public async Task<List<IoTEdgeRelease>> GetReleasesAsync(ReleaseDefinitionId definitionId, string branchName, int top = 200)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(branchName, nameof(branchName));
            ValidationUtil.ThrowIfNonPositive(top, nameof(top));

            // TODO: need to think about how to handle unexpected exception during REST API call
            string requestPath = string.Format(ReleasePathSegmentFormat, DevOpsAccessSetting.AzureOrganization, DevOpsAccessSetting.AzureProject);
            IFlurlRequest listReleasesRequest = DevOpsAccessSetting.ReleaseManagementBaseUrl
                .AppendPathSegment(requestPath)
                .SetQueryParam("definitionId", definitionId.IdString())
                .SetQueryParam("queryOrder", "descending")
                .SetQueryParam("$top", top)
                .SetQueryParam("api-version", "5.1")
                .SetQueryParam("sourceBranchFilter", branchName)
                .WithBasicAuth(string.Empty, this.accessSetting.MsazurePAT);

            string releasesJson = await listReleasesRequest.GetStringAsync().ConfigureAwait(false);
            JObject releasesJObject = JObject.Parse(releasesJson);

            if (!releasesJObject.ContainsKey("count") || (int)releasesJObject["count"] <= 0)
            {
                return new List<IoTEdgeRelease>();
            }

            VstsRelease[] vstsReleases = JsonConvert.DeserializeObject<VstsRelease[]>(releasesJObject["value"].ToString());
            var iotEdgeReleases = new List<IoTEdgeRelease>();

            foreach (VstsRelease vstsRelease in vstsReleases)
            {
                IFlurlRequest getReleaseRequest = DevOpsAccessSetting.ReleaseManagementBaseUrl
                    .AppendPathSegment(requestPath)
                    .SetQueryParam("api-version", "5.1")
                    .SetQueryParam("releaseId", vstsRelease.Id)
                    .WithBasicAuth(string.Empty, this.accessSetting.MsazurePAT);

                string releaseJson = await getReleaseRequest.GetStringAsync().ConfigureAwait(false);

                try
                {
                    VstsRelease releaseWithDetails = JsonConvert.DeserializeObject<VstsRelease>(releaseJson);
                    iotEdgeReleases.Add(IoTEdgeRelease.Create(releaseWithDetails, branchName));
                }
                catch (System.Exception ex)
                {
                    // TODO: log exception
                    Console.WriteLine(ex.ToString());
                }
            }

            return iotEdgeReleases;
        }
    }
}
