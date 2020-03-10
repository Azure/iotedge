// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Flurl;
    using Flurl.Http;
    using Newtonsoft.Json.Linq;

    public class BugManagement
    {
        const string WorkItemPathSegmentFormat = "{0}/{1}/{2}/{3}/_apis/wit/wiql/{4}";

        readonly DevOpsAccessSetting accessSetting;

        public BugManagement(DevOpsAccessSetting accessSetting)
        {
            this.accessSetting = accessSetting;
        }

        /// <summary>
        /// This method is used to execute a Dev Ops work item query and get the number of bugs for a given query. 
        /// If result is not found for a query Id, it will return 0.
        /// Note: there is no validation of work item query ids.
        /// Reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/wit/wiql/get?view=azure-devops-rest-5.1
        /// </summary>
        /// <param name="bugQueryId">bug query id from azure dev ops shared queries</param>
        /// <returns>Number of bugs output by query</returns>
        public async Task<int> GetBugsQuery(string bugQueryId)
        {
            ValidationUtil.ThrowIfNullOrEmptySet(bugQueryId, nameof(bugQueryId));

            // TODO: need to think about how to handle unexpected exception during REST API call
            string requestPath = string.Format(WorkItemPathSegmentFormat, DevOpsAccessSetting.BaseUrl, this.accessSetting.Organization, this.accessSetting.Project, this.accessSetting.Team, bugQueryId);
            IFlurlRequest workItemQueryRequest = ((Url)requestPath)
                .WithBasicAuth(string.Empty, this.accessSetting.PersonalAccessToken);

            string resultJson = await workItemQueryRequest.GetStringAsync().ConfigureAwait(false);
            JObject result = JObject.Parse(resultJson);

            if (!result.ContainsKey("queryType"))
            {
                return 0; 
            }
            else
            {
                return result["workItems"].Count();
            }
        }
    }
}
