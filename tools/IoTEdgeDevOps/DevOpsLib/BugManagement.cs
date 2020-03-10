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
        /// Reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/wit/wiql/get?view=azure-devops-rest-5.1
        /// </summary>
        /// <param name="bugQueryId">bug query id from azure dev ops shared queries</param>
        /// <returns>Number of bugs output by query</returns>
        public async Task<int> GetBugsQuery(string bugQueryId)
        {
            ValidationUtil.ThrowIfNullOrEmptySet(bugQueryId, nameof(bugQueryId));

            // TODO: need to think about how to handle unexpected exception during REST API call
            string requestPath = string.Format(WorkItemPathSegmentFormat, this.accessSetting.Organization, this.accessSetting.Project, this.accessSetting.Team, bugQueryId);
            IFlurlRequest latestBuildRequest = GetBugsRequestUri(requestPath)
                .WithBasicAuth(string.Empty, this.accessSetting.PersonalAccessToken);

            string resultJson = await latestBuildRequest.GetStringAsync().ConfigureAwait(false);
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

        private static Url GetBugsRequestUri(string requestPath)
        {
            Url requestUri = DevOpsAccessSetting.BaseUrl.AppendPathSegment(requestPath);
            return requestUri;
        }
    }
}
