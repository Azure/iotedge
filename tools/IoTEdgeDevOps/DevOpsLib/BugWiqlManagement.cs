// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Flurl;
    using Flurl.Http;
    using Newtonsoft.Json.Linq;

    public class BugWiqlManagement
    {
        const string WorkItemPathSegmentFormat = "{0}/{1}/{2}/{3}/_apis/wit/wiql";

        readonly DevOpsAccessSetting accessSetting;

        public BugWiqlManagement(DevOpsAccessSetting accessSetting)
        {
            this.accessSetting = accessSetting;
        }

        /// <summary>
        /// This method is used to execute a Dev Ops work item query and get the number of bugs for a given query.
        /// If result is not found for a query, it will return 0.
        /// Reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/wit/wiql/query%20by%20wiql?view=azure-devops-rest-5.1
        /// </summary>
        /// <param name="bugQuery">Bug query object representing vsts shared queries</param>
        /// <returns>Number of bugs output by query</returns>
        public async Task<int> GetBugsCountAsync(BugWiqlQuery bugQuery)
        {
            // TODO: need to think about how to handle unexpected exception during REST API call
            string requestPath = string.Format(WorkItemPathSegmentFormat, DevOpsAccessSetting.BaseUrl, DevOpsAccessSetting.AzureOrganization, DevOpsAccessSetting.AzureProject, DevOpsAccessSetting.IoTTeam);
            IFlurlRequest workItemQueryRequest = ((Url)requestPath)
                .WithBasicAuth(string.Empty, this.accessSetting.MsazurePAT)
                .SetQueryParam("api-version", "5.1");

            JObject result;
            try
            {
                IFlurlResponse response = await workItemQueryRequest
                    .PostJsonAsync(new { query = bugQuery.GetWiqlFromConfiguration() });

                result = await response.GetJsonAsync<JObject>();
            }
            catch (FlurlHttpException e)
            {
                Console.WriteLine($"Failed making call to vsts work item api: {e.Message}");
                Console.WriteLine(e.Call.RequestBody);
                Console.WriteLine(e.Call.Response.StatusCode);
                Console.WriteLine(e.Call.Response.ResponseMessage);
                return 0;
            }

            if (!result.ContainsKey("queryType"))
            {
                return 0;
            }

            return result["workItems"].Count();
        }
    }
}
